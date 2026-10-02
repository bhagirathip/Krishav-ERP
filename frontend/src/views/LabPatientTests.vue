<script setup>
import { computed, onMounted, ref } from 'vue';
import { api, assetUrl } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import SearchableSelect from '../components/SearchableSelect.vue';
import { useGrid } from '../composables/useGrid';
import { ageText } from '../utils/age';
import { looksLikeHtml, legacyNoteToHtml, sanitizeNoteHtml } from '../utils/richNote';

const tests=ref([]),orders=ref([]),patients=ref([]),discounts=ref([]),showOrder=ref(false),showTests=ref(false),showResult=ref(false),selectedOrder=ref(null),orderTests=ref([]),resultData=ref(null);
const pageSize=10;
const order=ref({patientId:null,roundOff:0,tests:[{labTestId:null,discountTypeId:null}]});
const {search,page,pageCount,pagedRows:pagedOrders,sortedRows,sortBy,sortIndicator}=useGrid(orders,pageSize);
const patientOptions=computed(()=>patients.value.map(p=>({value:p.id,label:`${p.name} · ${p.patientCode}`})));
const testOptions=computed(()=>tests.value.map(t=>({value:t.id,label:t.name})));
// The test note is now rich HTML (or, for tests saved before that editor
// shipped, still the old plain-text convention) - render it the same way
// the print view does rather than showing raw markup as escaped text.
const resultNoteHtml=computed(()=>noteDisplayHtml(resultData.value?.testNote));

async function load(){const [t,o,p,d]=await Promise.all([api.get('/lab/tests'),api.get('/lab/orders'),api.get('/patients'),api.get('/discounts',{params:{scope:'Individual',activeOnly:true}})]);tests.value=t.data;orders.value=o.data;patients.value=p.data;discounts.value=d.data}
function newOrder(){order.value={patientId:null,roundOff:0,tests:[{labTestId:null,discountTypeId:null}]};showOrder.value=true}
function addTestLine(){order.value.tests.push({labTestId:null,discountTypeId:null})}
function removeTestLine(i){order.value.tests.splice(i,1);if(!order.value.tests.length)addTestLine()}
function testObj(l){return tests.value.find(t=>t.id===l.labTestId)}
function linePrice(l){return Number(testObj(l)?.price||0)}
function lineDisc(l){const p=linePrice(l),d=discounts.value.find(x=>x.id===l.discountTypeId);if(!d)return 0;const v=Number(d.value||0);return d.discountMode==='Amount'?Math.min(p,v):p*Math.min(100,Math.max(0,v))/100}
function beforeTotal(){return order.value.tests.reduce((a,l)=>a+linePrice(l),0)}
function afterTotal(){return order.value.tests.reduce((a,l)=>a+linePrice(l)-lineDisc(l),0)}
// Small manual nudge (-9..9) folded into the bill total, e.g. 448 -> 450; never a separate print line.
function finalTotal(){return afterTotal()+Number(order.value.roundOff||0)}
async function saveOrder(){if(!order.value.patientId)return alert('Select patient.');if(order.value.tests.some(x=>!x.labTestId))return alert('Select test for every row.');if(order.value.roundOff<-9||order.value.roundOff>9)return alert('Round off must be between -9 and 9.');await api.post('/lab/orders',order.value);showOrder.value=false;await load()}
async function openOrder(x){selectedOrder.value=x;orderTests.value=(await api.get(`/lab/orders/${x.id}/tests`)).data;showTests.value=true}
async function openResult(t){
  const data=(await api.get(`/lab/order-tests/${t.id}`)).data;
  const schema=data.resultSchema||{};
  resultData.value={
    ...data,
    columns:Array.isArray(schema.columns)?schema.columns:[],
    rows:Array.isArray(schema.rows)?schema.rows:[]
  };
  showResult.value=true
}
async function saveResults(){
  const schema={
    columns:resultData.value.columns,
    rows:resultData.value.rows
  };
  await api.put(
    `/lab/order-tests/${resultData.value.orderTestId}/results`,
    {schemaJson:JSON.stringify(schema)}
  );
  showResult.value=false;
  await openOrder(selectedOrder.value);
  await load()
}
function formatDateTime(value){
  if(!value)return '-';
  const d=new Date(value);
  if(Number.isNaN(d.getTime()))return '-';
  const pad=n=>String(n).padStart(2,'0');
  return `${pad(d.getDate())}-${pad(d.getMonth()+1)}-${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function findValueColumnId(columns){
  // Parameter/Range/Unit are pre-filled from the test master when a test is
  // ordered, so only the actual result column reliably tells us whether a
  // row has been filled in yet - find it by name (same heuristic LabMaster.vue
  // uses for its own "Default Value" column), not by a fixed id.
  const col=columns.find(c=>{
    const n=(c.name||'').trim().toLowerCase();
    return n.includes('value')||n.includes('result');
  });
  return col?col.id:null;
}

function findRangeColumnId(columns){
  const col=columns.find(c=>(c.name||'').trim().toLowerCase().includes('range'));
  return col?col.id:null;
}

// Reference ranges come in the "low-high", "<high" or ">low" forms (same
// convention as the BM500 analyzer's OBX-7 field), so this checks a numeric
// result against whichever form is present. Non-numeric results or ranges
// (qualitative tests, blank cells) are left alone rather than guessed at.
function isResultOutOfRange(value,range){
  const numeric=parseFloat(String(value??'').trim());
  const text=String(range??'').trim();
  if(!text||Number.isNaN(numeric))return false;

  if(text.startsWith('<')){
    const upper=parseFloat(text.slice(1));
    return !Number.isNaN(upper)&&numeric>=upper;
  }
  if(text.startsWith('>')){
    const lower=parseFloat(text.slice(1));
    return !Number.isNaN(lower)&&numeric<=lower;
  }

  const parts=text.split('-').map(s=>s.trim()).filter(Boolean);
  if(parts.length===2){
    const low=parseFloat(parts[0]),high=parseFloat(parts[1]);
    if(!Number.isNaN(low)&&!Number.isNaN(high))return numeric<low||numeric>high;
  }
  return false;
}

function isMethodColumnName(name){
  return (name||'').trim().toLowerCase().includes('method');
}

// Lets a technician manually bold part of a value by wrapping it in **like
// this**, the same convention used for the Note field's Bold button - applied
// everywhere test/result text is printed, not just the Note row.
function renderWithBold(value){
  return escapeHtml(value).replace(/\*\*([^*]+)\*\*/g,'<b>$1</b>');
}

// A note is either real HTML (saved from the Note rich-text editor) or, for
// tests saved before that editor shipped, still the old markdown-lite plain
// text convention (**bold**, *italic*, "- " bullets) - render whichever one
// it actually is.
function noteDisplayHtml(value){
  if(!value)return '';
  return looksLikeHtml(value)?sanitizeNoteHtml(value):legacyNoteToHtml(value);
}

// The static table is a separate small reference table on the test master
// (its own columns/rows, not patient-result data) that prints below the
// Note, read live from the master rather than snapshotted per order-test.
function buildStaticTableHtml(staticTableJson,colSpan){
  if(!staticTableJson)return '';
  let table;
  try{table=JSON.parse(staticTableJson)}catch{return ''}
  if(!table||!Array.isArray(table.columns)||!table.columns.length||!Array.isArray(table.rows))return '';

  const headHtml=table.columns.map(c=>`<th>${escapeHtml(c.name||'')}</th>`).join('');
  const bodyHtml=table.rows.map(row=>{
    const cells=table.columns.map(c=>`<td>${renderWithBold(row.values?.[c.id]??'')}</td>`).join('');
    return `<tr>${cells}</tr>`;
  }).join('');
  if(!bodyHtml)return '';

  return `<tr class="print-atomic-row static-table-row"><td colspan="${colSpan}"><table class="static-ref-table"><thead><tr>${headHtml}</tr></thead><tbody>${bodyHtml}</tbody></table></td></tr>`;
}

// One shared result table for one or more tests: the column header row (No.,
// Investigation, Unit, Range, ...) is printed once, then each test contributes
// a name row followed by its own parameter rows - instead of a separate table
// with its own header per test. The Sl. No. column counts continuously across
// every test in the merged print, rather than restarting at 1 for each test.
function buildMergedTestsHtml(results){
  const canonicalCols=[];
  const seen=new Set();
  results.forEach(data=>{
    const columns=Array.isArray(data.resultSchema?.columns)?data.resultSchema.columns:[];
    columns.forEach(c=>{
      const name=c.name||'';
      if(!seen.has(name)){seen.add(name);canonicalCols.push(name)}
    });
  });

  // A Method column (when present) isn't printed as its own table column -
  // it's shown as a smaller sub-line under the Investigation column instead.
  const displayCols=canonicalCols.filter(name=>!isMethodColumnName(name));
  const investigationName=displayCols.find(name=>name.trim().toLowerCase().includes('investigation'))||displayCols[0];

  const headerHtml=`<th class="no-col">No.</th>${displayCols.map(name=>`<th>${escapeHtml(name)}</th>`).join('')}`;

  let rowCounter=0;
  const bodyHtml=results.map(data=>{
    const schema=data.resultSchema||{};
    const columns=Array.isArray(schema.columns)?schema.columns:[];
    const nameToId={};
    columns.forEach(c=>{nameToId[c.name||'']=c.id});
    const valueColumnId=findValueColumnId(columns);
    const rangeColumnId=findRangeColumnId(columns);
    const methodName=canonicalCols.find(name=>isMethodColumnName(name)&&nameToId[name]);
    // Skip parameters that have no result entered yet, but always keep
    // group-header rows (even with a blank name) since they're section
    // dividers, not parameters, and have no .values to check.
    const rows=(Array.isArray(schema.rows)?schema.rows:[])
      .filter(row=>row.isGroupHeader||(valueColumnId
        ? String(row.values?.[valueColumnId]??'').trim()!==''
        : columns.some(c=>String(row.values?.[c.id]??'').trim()!=='')));
    const hasDataRows=rows.some(row=>!row.isGroupHeader);

    const nameRowHtml=`<tr class="print-atomic-row test-name-row"><td class="no-col"></td><td colspan="${Math.max(1,displayCols.length)}">${renderWithBold(data.test?.name||'')}</td></tr>`;
    const rowsHtml=hasDataRows
      ? rows.map(row=>{
          if(row.isGroupHeader){
            return `<tr class="print-atomic-row group-header-row"><td class="no-col"></td><td colspan="${Math.max(1,displayCols.length)}">${renderWithBold(row.groupName||'')}</td></tr>`;
          }
          const outOfRange=valueColumnId&&rangeColumnId
            &&isResultOutOfRange(row.values?.[valueColumnId],row.values?.[rangeColumnId]);
          rowCounter++;
          const methodValue=methodName?String(row.values?.[nameToId[methodName]]??'').trim():'';
          const cells=displayCols.map(name=>{
            const cellValue=row.values?.[nameToId[name]]??'';
            const methodLine=(name===investigationName&&methodValue)
              ?`<div class="method-line">${renderWithBold(methodValue)}</div>`
              :'';
            return `<td>${renderWithBold(cellValue)}${methodLine}</td>`;
          }).join('');
          return `<tr class="print-atomic-row${outOfRange?' out-of-range':''}"><td class="no-col">${rowCounter}</td>${cells}</tr>`;
        }).join('')
      : `<tr class="print-atomic-row"><td class="no-col"></td><td colspan="${Math.max(1,displayCols.length)}" class="muted-cell">No results entered yet.</td></tr>`;
    // No "Note:" label and no leading no-col cell - whatever the user typed
    // is left to stand on its own, starting flush from the very left edge
    // (under Sl No) rather than indented under the Investigation column.
    const noteContent=noteDisplayHtml(data.test?.note);
    const noteHtml=noteContent
      ? `<tr class="print-atomic-row note-row"><td colspan="${Math.max(1,displayCols.length)+1}" class="note-cell">${noteContent}</td></tr>`
      : '';
    const staticTableHtml=buildStaticTableHtml(data.test?.staticTableJson,Math.max(1,displayCols.length)+1);

    return nameRowHtml+rowsHtml+noteHtml+staticTableHtml;
  }).join('');

  // Printed once at the end of the whole merged block (not per test), full
  // width so it's centered on the page rather than just the data columns.
  const stayHealthyHtml=`<tr class="print-atomic-row stay-healthy-row"><td colspan="${Math.max(1,displayCols.length)+1}">***** Stay Healthy *****</td></tr>`;

  return `<table class="result-table">
    <thead><tr>${headerHtml}</tr></thead>
    <tbody>${bodyHtml}${stayHealthyHtml}</tbody>
  </table>`;
}

// Splits a batch of tests into print blocks by their Lab Test Master Group:
// tests sharing the same (non-empty) group are merged onto one shared table
// together, in the order their group was first encountered; a test with no
// group always gets its own separate block instead of merging with anything.
function partitionByGroup(results){
  const groupedBlocks=new Map();
  const blocks=[];

  results.forEach(data=>{
    const groupName=(data.test?.group||'').trim();
    if(!groupName){
      blocks.push([data]);
      return;
    }
    if(!groupedBlocks.has(groupName)){
      const block=[];
      groupedBlocks.set(groupName,block);
      blocks.push(block);
    }
    groupedBlocks.get(groupName).push(data);
  });

  return blocks;
}

// Renders each group's block as its own <table>, wrapped so every block
// after the first forces a fresh printed page - a group's tests share a
// page (and overflow across more pages together like any merged print), but
// never bleed onto the same page as a different group or an ungrouped test.
function buildGroupedBlocksHtml(results){
  return partitionByGroup(results)
    .map(block=>`<div class="test-group-block">${buildMergedTestsHtml(block)}</div>`)
    .join('');
}

// Wraps one or more test blocks with the shared header image, patient info
// (styled as plain label rows, not a bordered box, per the reference report)
// and a signatory footer. The header/footer used to be `position:fixed`
// elements offset by the negative of the @page margin, which is a commonly
// suggested trick for repeating print stationery - but verified against
// Chromium's actual print/PDF engine (not just an on-screen preview), that
// trick renders NOTHING: fixed-position elements are not drawn into the
// page margin box at all, so the header/footer silently vanished from every
// real print/PDF. The one pattern that verifiably repeats a header AND a
// footer on every printed page in Chromium is an outer <table> with
// <thead>/<tfoot> set to display:table-header-group/table-footer-group -
// the whole document is that one table, with all page content in its
// <tbody>.
function buildLabReportHtml({hospital,patient,order,reportDate,blocksHtml}){
  return `<!doctype html>
  <html>
    <head>
      <title></title>
      <style>
        @page{size:A4;margin:10mm}
        *{box-sizing:border-box}
        html,body{margin:0;padding:0;font-family:"Times New Roman",serif;color:#111}
        body{font-size:14.5px;counter-reset:page 1}

        table.page-frame{width:100%;border-collapse:collapse}
        .page-frame thead{display:table-header-group}
        .page-frame tfoot{display:table-footer-group}
        .page-frame>thead>tr>td,.page-frame>tfoot>tr>td,.page-frame>tbody>tr>td{padding:0}
        .page-frame>tbody>tr>td{vertical-align:top}

        /* Repeated laboratory stationery. */
        .lab-page-header img{display:block;width:100%;height:42mm;object-fit:fill}

        .lab-page-footer{border-top:1px solid #222;padding:3mm 1mm 2mm;display:grid;grid-template-columns:1.4fr 1fr;column-gap:8mm;align-items:start;font-size:12px;line-height:1.35}
        .footer-meta div{margin:0 0 1px}
        .footer-meta b{font-weight:400}
        .signature-block{text-align:right;align-self:start}
        .signature-block img{display:block;max-height:11mm;max-width:42mm;margin:0 auto -1mm}
        .signature-block .sig-label{margin-bottom:1px}
        .signature-block .sig-name{font-weight:700;font-size:12.5px}
        .signature-block .sig-qual{font-size:12px}

        /* Printed once, directly below the configured lab header. */
        .patient-info{border-top:1.5px solid #222;border-bottom:1.5px solid #222;margin:4mm 0;padding:2mm 1mm}
        .pi-row{display:grid;grid-template-columns:1fr 1fr;gap:0 12mm;padding:1.1mm 0;font-size:13.5px}
        .pi-row b{display:inline-block;min-width:27mm;font-weight:700}

        /* Each Lab Test Master Group prints as its own block; every block
           after the first is forced onto a fresh page so one group's tests
           never share a page with a different group or an ungrouped test. */
        .test-group-block+.test-group-block{break-before:page;page-break-before:always}

        /* The column header (No./Investigation/Unit/Range/...) is printed once
           for the whole table; each test contributes a name row (kept with its
           first result row) followed by its own parameter rows. */
        table.result-table{width:100%;border-collapse:collapse;table-layout:auto}
        .result-table thead{display:table-header-group}
        .result-table thead th{background:#91C2F7;color:#fff;padding:2.2mm 2mm;text-align:left;font-family:Arial,sans-serif;font-size:12.5px;font-weight:700;border:0}
        .result-table tbody td{padding:1.25mm 2mm;text-align:left;vertical-align:top;word-break:break-word;white-space:pre-line;font-size:13px;border:0}
        .result-table tbody tr{break-inside:avoid;page-break-inside:avoid}
        .result-table tbody tr.out-of-range td{font-weight:700}
        /* No border between tests - a bigger, bolder H1-style name plus
           generous top spacing is what tells two tests apart on the page. */
        .test-name-row td{font-weight:700;font-size:22px;text-decoration:underline;padding-top:7mm;border-top:0}
        /* Same font/size as the test name, but never underlined - only the
           Test Name itself is underlined, so a group header stays visually
           distinct as a sub-section divider rather than looking like a test. */
        .group-header-row td{font-weight:700;font-size:22px;text-decoration:none;padding-top:5mm;border-top:0}
        .method-line{font-size:11px;color:#333;font-style:italic;margin-top:0.6mm;white-space:pre-line}
        .no-col{width:10mm;text-align:center!important}
        .muted-cell{color:#777;font-style:italic}
        .note-cell{color:#444;font-size:9.5px}
        .note-cell div{margin:0.5mm 0}
        .note-list,.note-cell ul,.note-cell ol{margin:1mm 0 0.5mm;padding-left:5mm}
        .note-list li,.note-cell li{margin:0.5mm 0}
        /* A separate small static reference table from the test master,
           printed below the Note - its header uses the same blue as the
           main result table's header (No./Investigation/...) so it reads
           as part of the same report rather than a foreign element. */
        .static-table-row td{padding-top:1.5mm}
        table.static-ref-table{width:100%;border-collapse:collapse;margin-top:1mm}
        table.static-ref-table thead th{background:#91C2F7;color:#fff;padding:1.5mm 2mm;text-align:left;font-family:Arial,sans-serif;font-size:11px;font-weight:700;border:0}
        table.static-ref-table tbody td{padding:1.2mm 2mm;font-size:11px;border:1px solid #dbe4ef;vertical-align:top;word-break:break-word}
        .stay-healthy-row td{text-align:center!important;font-weight:700;padding-top:3mm;font-size:13px}

        /* One indicator prints at the bottom of every page, sitting flush
           against the footer's top rule line: P.T.O in the middle on every
           page but the last (there's nothing left to turn to), and this
           page's number over the total on every page. */
        .page-indicator-row td{padding:2mm 2mm 0}
        .page-indicator{display:grid;grid-template-columns:1fr 1fr 1fr;align-items:center;font-size:11px;font-weight:700}
        .page-indicator .pto{text-align:center}
        .page-indicator .pageno{text-align:right}
      </style>
    </head>
    <body>
      <table class="page-frame">
        <thead><tr><td class="lab-page-header">
          ${hospital.labHeader?`<img src="${assetUrl(hospital.labHeader)}">`:''}
        </td></tr></thead>

        <tfoot><tr><td>
          <div class="lab-page-footer">
            <div class="footer-meta">
              <div><b>CRM No :</b> ${escapeHtml(patient.patientCode||order?.orderNumber||'')}</div>
              <div><b>Sample Recd. Time:</b> ${formatDateTime(order?.createdAtUtc)}</div>
              <div><b>Report Time:</b> ${formatDateTime(reportDate)}</div>
              <div><b>Patient Name:</b> ${escapeHtml(patient.name||'')}</div>
            </div>
            <div class="signature-block">
              ${hospital.labSignature?`<img src="${assetUrl(hospital.labSignature)}">`:''}
              <div class="sig-label">Authorized Signatory</div>
              <div class="sig-name">${escapeHtml(hospital.labSignatoryName||'')}</div>
              <div class="sig-qual">${escapeHtml(hospital.labSignatoryQualification||'')}</div>
            </div>
          </div>
        </td></tr></tfoot>

        <tbody><tr><td>
          <main>
            <div class="patient-info">
              <div class="pi-row"><span><b>Name:</b> ${escapeHtml(patient.name||'')}</span><span><b>Age/Gender:</b> ${escapeHtml(ageText(patient))} / ${escapeHtml(patient.gender||'')}</span></div>
              <div class="pi-row"><span><b>Referred By:</b> N.A</span><span><b>Client Name:</b> N.A</span></div>
              <div class="pi-row"><span><b>Collection Date:</b> ${formatDateTime(order?.createdAtUtc)}</span><span><b>Report Release Date:</b> ${formatDateTime(reportDate)}</span></div>
            </div>
            ${blocksHtml}
          </main>
        </td></tr></tbody>
      </table>

      <script>
        const waitForImages=()=>Promise.all(Array.from(document.images).map(img=>img.complete?Promise.resolve():new Promise(resolve=>{img.onload=img.onerror=resolve})));

        // A table's rows never stretch to fill a page on their own, so a
        // short page's "P.T.O / Page X/Y" line (and, on the last page, the
        // footer) would otherwise sit right under the last result instead of
        // at the bottom of the page - and knowing where to put that line at
        // all means knowing exactly where each page will break BEFORE the
        // browser paginates it. All three are solved together: walk every
        // atomic row (test name / result / note) in document order,
        // simulate the same page-by-page packing the print engine will do
        // (a Group's first row can share a page with the patient info panel
        // or a still-open previous page; every later Group is forced onto a
        // fresh page; anything taller than one page's budget just keeps
        // flowing onto ordinary full pages), and at every resulting page
        // boundary insert a spacer sized to whatever's left on that page
        // followed by a real indicator row with a *forced* break right after
        // it - turning what would have been the browser's own natural
        // reflow into a break we already knew was coming, so our page count
        // and the real one always agree. The last page shows "End Result"
        // instead of "P.T.O" (there's nothing left to turn to).
        function paginatePrint(){
          // Idempotent: drop anything a previous call already inserted
          // before recomputing, so calling this more than once never stacks
          // up duplicate fillers/indicators.
          document.querySelectorAll('.page-filler-row,.page-indicator-row').forEach(el=>el.remove());

          const header=document.querySelector('.lab-page-header');
          const footer=document.querySelector('.lab-page-footer');
          const patientInfo=document.querySelector('.patient-info');
          const blocks=[...document.querySelectorAll('.test-group-block')];
          const rows=[...document.querySelectorAll('.print-atomic-row')];
          if(!header||!footer||!rows.length)return;

          const blockIndex=new Map(blocks.map((b,i)=>[b,i]));
          const mmToPx=96/25.4;
          const indicatorPx=7*mmToPx; // the "P.T.O / Page X/Y" line's own fixed, single-line height
          // A small safety margin: the print engine's own pagination doesn't
          // line up with this on-screen measurement to the sub-pixel, and
          // filling all the way to the calculated edge risks rounding error
          // spilling a sliver of content onto a spurious extra blank page -
          // better to leave a few mm of slack above the footer than that.
          const safetyPx=20*mmToPx;
          const pageContentPx=277*mmToPx; // A4 height minus the 10mm @page margin on both edges
          const headerPx=header.getBoundingClientRect().height;
          const footerPx=footer.getBoundingClientRect().height;
          const perPageBodyPx=pageContentPx-headerPx-footerPx-indicatorPx-safetyPx;
          if(perPageBodyPx<=0)return;

          let usedOnCurrentPage=patientInfo?patientInfo.getBoundingClientRect().height:0;
          let totalPages=1;
          const breaks=[];
          // Tracks the last row actually kept in the document, since a
          // stranded Stay Healthy line can be dropped mid-walk below - any
          // break/indicator must attach after this, never after a row index
          // that might have just been removed from the page.
          let lastSurvivingRow=null;
          let lastSurvivingGroup=null;

          rows.forEach(row=>{
            const rowGroup=blockIndex.get(row.closest('.test-group-block'));
            if(lastSurvivingRow&&rowGroup!==lastSurvivingGroup){
              breaks.push({afterRow:lastSurvivingRow,pageNumber:totalPages,fillerPx:perPageBodyPx-usedOnCurrentPage});
              totalPages++;
              usedOnCurrentPage=0;
            }
            const rowPx=row.getBoundingClientRect().height;
            if(usedOnCurrentPage+rowPx<=perPageBodyPx){
              usedOnCurrentPage+=rowPx;
            }else if(row.classList.contains('stay-healthy-row')){
              // It's always the last row of its block, so overflowing here
              // means it would land completely alone at the top of a fresh
              // page - not worth a whole page for one centered line, so
              // drop it instead of forcing a break for it.
              row.remove();
              return;
            }else if(lastSurvivingRow){
              breaks.push({afterRow:lastSurvivingRow,pageNumber:totalPages,fillerPx:perPageBodyPx-usedOnCurrentPage});
              totalPages++;
              usedOnCurrentPage=rowPx;
            }else{
              usedOnCurrentPage+=rowPx;
            }
            lastSurvivingRow=row;
            lastSurvivingGroup=rowGroup;
          });

          function insertPageEnd(afterRow,pageNumber,fillerPx,isIntermediate){
            const table=afterRow.closest('table');
            const colCount=table?.querySelectorAll('thead th').length||1;

            if(fillerPx>1){
              const fillerRow=document.createElement('tr');
              fillerRow.className='page-filler-row';
              const fillerTd=document.createElement('td');
              fillerTd.colSpan=colCount;
              fillerTd.style.height=fillerPx+'px';
              fillerTd.style.padding='0';
              fillerRow.appendChild(fillerTd);
              afterRow.after(fillerRow);
              afterRow=fillerRow;
            }

            const indicatorRow=document.createElement('tr');
            indicatorRow.className='page-indicator-row';
            if(isIntermediate){
              indicatorRow.style.breakAfter='page';
              indicatorRow.style.pageBreakAfter='always';
            }
            const indicatorTd=document.createElement('td');
            indicatorTd.colSpan=colCount;
            indicatorTd.innerHTML='<div class="page-indicator"><span></span><span class="pto">'+(isIntermediate?'P.T.O':'End Result')+'</span><span class="pageno">Page '+pageNumber+'/'+totalPages+'</span></div>';
            indicatorRow.appendChild(indicatorTd);
            afterRow.after(indicatorRow);
          }

          breaks.forEach(({afterRow,pageNumber,fillerPx})=>insertPageEnd(afterRow,pageNumber,fillerPx,true));
          if(lastSurvivingRow)insertPageEnd(lastSurvivingRow,totalPages,perPageBodyPx-usedOnCurrentPage,false);
        }

        window.onload=async()=>{
          await waitForImages();
          paginatePrint();
          setTimeout(()=>window.print(),100);
        };
        window.onafterprint=()=>window.parent.postMessage('lab-print','*');
      <\/script>
    </body>
  </html>`;
}

function openPrintFrame(html){
  const f=document.createElement('iframe');
  f.style.position='fixed';
  f.style.left='-10000px';
  f.style.top='0';
  // Matches the printed page's content width (A4 minus the 10mm @page
  // margins) rather than 0x0 - buildLabReportHtml's inline script measures
  // real element heights before printing (to size the last page's footer
  // spacer), and that only lines up with what actually gets printed if the
  // frame it's measuring wraps text at the same width print will use.
  f.style.width='190mm';
  f.style.height='10000px';
  f.style.border='0';
  document.body.appendChild(f);

  const d=f.contentWindow.document;
  d.open();
  d.write(html);
  d.close();

  const h=e=>{
    if(e.data==='lab-print'){
      f.remove();
      window.removeEventListener('message',h)
    }
  };
  window.addEventListener('message',h)
}

async function printResult(t){
  const {data}=await api.get(`/lab/order-tests/${t.id}/print`);
  const html=buildLabReportHtml({
    hospital:data.hospital||{},
    patient:data.patient||{},
    order:data.order,
    reportDate:data.resultUpdatedAtUtc,
    blocksHtml:buildGroupedBlocksHtml([data])
  });
  openPrintFrame(html);
}

// Pulls every given order-test's print data and merges them into a single
// continuous document, so multiple short tests can share a page while a
// longer set naturally flows onto more pages.
async function printMergedResults(orderTestRows){
  if(!orderTestRows.length)return;

  const results=await Promise.all(
    orderTestRows.map(t=>api.get(`/lab/order-tests/${t.id}/print`).then(r=>r.data))
  );

  const reportDate=results.reduce((latest,data)=>{
    if(!data.resultUpdatedAtUtc)return latest;
    return !latest || new Date(data.resultUpdatedAtUtc)>new Date(latest) ? data.resultUpdatedAtUtc : latest;
  },null);

  const html=buildLabReportHtml({
    hospital:results[0]?.hospital||{},
    patient:results[0]?.patient||{},
    order:results[0]?.order,
    reportDate,
    blocksHtml:buildGroupedBlocksHtml(results)
  });
  openPrintFrame(html);
}

// Print All Results button inside the order's test list modal.
async function printAllResults(){
  await printMergedResults(orderTests.value);
}

// Print button directly on the orders grid row - no need to open the modal first.
async function printOrderResults(x){
  const rows=(await api.get(`/lab/orders/${x.id}/tests`)).data;
  await printMergedResults(rows);
}

async function deleteOrderTest(t){
  if(!confirm(`Remove ${t.testName} from this order?`))return;
  try{
    await api.delete(`/lab/order-tests/${t.id}`);
    await openOrder(selectedOrder.value);
    await load();
  }catch(error){
    alert(error.response?.data?.message||'Unable to delete this test.');
  }
}

// Delete button directly on the orders grid row - removes the whole order
// (every test in it) and its bill, e.g. to clear out a duplicate entry.
async function deleteOrder(x){
  if(!confirm(`Delete order ${x.orderNumber} for ${x.patientName}? This removes all ${x.testCount} test(s) in it.`))return;
  try{
    await api.delete(`/lab/orders/${x.id}`);
    await load();
  }catch(error){
    alert(error.response?.data?.message||'Unable to delete this order.');
  }
}

function escapeHtml(value){
  return String(value??'')
    .replaceAll('&','&amp;')
    .replaceAll('<','&lt;')
    .replaceAll('>','&gt;')
    .replaceAll('"','&quot;')
    .replaceAll("'","&#039;")
}

onMounted(load)
</script>

<template>
<h1>Patient Lab Tests</h1>
<div class="toolbar"><button v-if="can('LAB_PATIENT_TESTS','add')" @click="newOrder">+ Add Test Patient</button></div>
<div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all patient lab order fields..." /></div>
<table class="table"><tr><th class="sortable" @click="sortBy('orderNumber')">Order <span class="sort-indicator">{{sortIndicator('orderNumber')}}</span></th><th class="sortable" @click="sortBy('patientName')">Patient <span class="sort-indicator">{{sortIndicator('patientName')}}</span></th><th class="sortable" @click="sortBy('createdAtUtc')">Date <span class="sort-indicator">{{sortIndicator('createdAtUtc')}}</span></th><th class="sortable" @click="sortBy('testCount')">Tests <span class="sort-indicator">{{sortIndicator('testCount')}}</span></th><th class="sortable" @click="sortBy('status')">Status <span class="sort-indicator">{{sortIndicator('status')}}</span></th><th></th></tr><tr v-for="x in pagedOrders" :key="x.id"><td>{{x.orderNumber}}</td><td>{{x.patientName}}<div class="muted">{{x.patientCode}}</div></td><td>{{new Date(x.createdAtUtc).toLocaleString()}}</td><td>{{x.testCount}}</td><td>{{x.status}}</td><td class="actions"><button @click="openOrder(x)">View / Update Results</button><button @click="printOrderResults(x)">Print</button><button class="danger-btn" @click="deleteOrder(x)">Delete</button></td></tr></table>
<Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page=$event" />
<div v-if="showOrder" class="modal-bg"><div class="modal modal-xl"><button class="modal-close-x" @click="showOrder=false">×</button><h2>Add Tests for Patient</h2><label>Patient *<SearchableSelect v-model="order.patientId" :options="patientOptions" placeholder="Type patient name or code..." /></label><table class="table" style="margin-top:16px"><tr><th>Test</th><th>Price</th><th>Approved Discount</th><th>Discount</th><th>After Discount</th><th></th></tr><tr v-for="(l,i) in order.tests" :key="i"><td><SearchableSelect v-model="l.labTestId" :options="testOptions" placeholder="Type test name..." /></td><td>₹{{linePrice(l).toFixed(2)}}</td><td><select v-model.number="l.discountTypeId"><option :value="null">No Discount</option><option v-for="d in discounts" :key="d.id" :value="d.id">{{d.name}} · {{d.discountMode==='Percent'?d.value+'%':'₹'+Number(d.value).toFixed(2)}}</option></select></td><td>₹{{lineDisc(l).toFixed(2)}}</td><td>₹{{(linePrice(l)-lineDisc(l)).toFixed(2)}}</td><td><button class="danger-btn" @click="removeTestLine(i)">×</button></td></tr></table><button @click="addTestLine">+ Add Test</button><div class="bill-total-box"><div><span>Before Discount</span><b>₹{{beforeTotal().toFixed(2)}}</b></div><div class="grand"><span>After Discount</span><b>₹{{afterTotal().toFixed(2)}}</b></div><div><span>Round Off (-9 to 9)</span><input v-model.number="order.roundOff" type="number" min="-9" max="9" step="1" style="width:80px;display:inline-block"></div><div class="grand"><span>Final Amount</span><b>₹{{finalTotal().toFixed(2)}}</b></div></div><div class="modal-actions"><button @click="saveOrder">Create Tests + Bill</button><button class="secondary" @click="showOrder=false">Cancel</button></div></div></div>
<div v-if="showTests" class="modal-bg"><div class="modal modal-xl"><button class="modal-close-x" @click="showTests=false">×</button><div class="page-head"><h2>{{selectedOrder.patientName}} · {{selectedOrder.orderNumber}}</h2><div class="actions"><button v-if="orderTests.length" @click="printAllResults">Print All Results</button><button class="secondary" @click="showTests=false">Close</button></div></div><table class="table"><tr><th>Test</th><th>Status</th><th></th></tr><tr v-for="t in orderTests" :key="t.id"><td>{{t.testName}}</td><td>{{t.status}}</td><td class="actions"><button @click="openResult(t)">Edit</button><button @click="printResult(t)">Print</button><button class="danger-btn" @click="deleteOrderTest(t)">Delete</button></td></tr></table><div class="modal-actions"><button class="secondary" @click="showTests=false">Close</button></div></div></div>
<div v-if="showResult" class="modal-bg">
  <div class="modal modal-xl">
    <button class="modal-close-x" @click="showResult=false">×</button>
    <div class="page-head">
      <div>
        <h2>
          {{resultData.testName}} · {{resultData.patient?.name}}
        </h2>

      </div>
      <button class="secondary" @click="showResult=false">
        Close
      </button>
    </div>

    <div
      v-if="!resultData.columns.length"
      class="error-box"
    >
      No Lab Test Master columns were found for this test.
      Please open Lab Test Master, save the test again, and reopen this result.
    </div>

    <div
      v-else
      class="lab-result-dynamic-wrap"
    >
      <table class="table lab-result-dynamic-table">
        <thead>
          <tr>
            <th
              v-for="column in resultData.columns"
              :key="column.id"
            >
              {{column.name}}
            </th>
          </tr>
        </thead>

        <tbody>
          <tr
            v-for="row in resultData.rows"
            :key="row.id"
          >
            <td
              v-for="column in resultData.columns"
              :key="column.id"
            >
              <textarea
                rows="2"
                class="lab-master-cell"
                v-model="row.values[column.id]"
                :placeholder="column.name"
              ></textarea>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="resultData.testNote" class="note-box">
      <b>Note:</b> <span v-html="resultNoteHtml"></span>
    </div>

    <div class="modal-actions">
      <button @click="saveResults">
        Save Results
      </button>
      <button class="secondary" @click="showResult=false">
        Cancel
      </button>
    </div>
  </div>
</div>
</template>