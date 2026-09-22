<script setup>
import { computed, onMounted, ref } from 'vue';
import { api, assetUrl } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const tests=ref([]),orders=ref([]),patients=ref([]),discounts=ref([]),showOrder=ref(false),showTests=ref(false),showResult=ref(false),selectedOrder=ref(null),orderTests=ref([]),resultData=ref(null);
const pageSize=10;
const order=ref({patientId:null,roundOff:0,tests:[{labTestId:null,discountTypeId:null}]});
const {search,page,pageCount,pagedRows:pagedOrders,sortedRows,sortBy,sortIndicator}=useGrid(orders,pageSize);

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

// One test's title + result table, no patient info/header/footer - reused by
// both the single-test print and the merged all-tests print.
function buildTestBlockHtml(data){
  const schema=data.resultSchema||{};
  const columns=Array.isArray(schema.columns)?schema.columns:[];
  const valueColumnId=findValueColumnId(columns);
  // Skip parameters that have no result entered yet.
  const rows=(Array.isArray(schema.rows)?schema.rows:[])
    .filter(row=>valueColumnId
      ? String(row.values?.[valueColumnId]??'').trim()!==''
      : columns.some(c=>String(row.values?.[c.id]??'').trim()!==''));

  const headerHtml=`<th class="no-col">No.</th>${columns.map(c=>`<th>${escapeHtml(c.name||'')}</th>`).join('')}`;
  const rowHtml=rows
    .map((row,index)=>`<tr><td class="no-col">${index+1}</td>${columns.map(c=>`<td>${escapeHtml(row.values?.[c.id]??'')}</td>`).join('')}</tr>`)
    .join('');

  return `<div class="test-block">
    <div class="test-title">${escapeHtml(data.test?.name||'')}</div>
    <table class="result-table">
      <thead><tr>${headerHtml}</tr></thead>
      <tbody>${rowHtml || `<tr><td class="no-col"></td><td colspan="${Math.max(1,columns.length)}" class="muted-cell">No results entered yet.</td></tr>`}</tbody>
    </table>
    ${data.test?.note?`<div class="note"><b>Note:</b> ${escapeHtml(data.test.note)}</div>`:''}
  </div>`;
}

// Wraps one or more test blocks with the shared header image, patient info
// (styled as plain label rows, not a bordered box, per the reference report)
// and a signatory footer. Uses a flex column stretched to a full A4 page so
// the footer sits pinned to the bottom of the page - a <table>/thead/tfoot
// was tried for a footer that repeats on every page, but it did not pin to
// the page bottom in practice (showed up right after the content instead),
// so this reverts to the simpler, reliably-bottom-pinned layout. The
// trade-off: on a report long enough to spill onto a second page, the
// footer only appears once at the very end, not repeated per page.
function buildLabReportHtml({hospital,patient,order,reportDate,blocksHtml}){
  return `<!doctype html>
  <html>
    <head>
      <title></title>
      <style>
        @page{size:A4;margin:0}
        *{box-sizing:border-box}
        html,body{height:100%}
        body{margin:0;font-family:Arial,sans-serif;color:#000}
        .page{min-height:297mm;display:flex;flex-direction:column}
        .header img{width:100%;height:42mm;object-fit:fill;display:block}
        .content{flex:1 0 auto;padding:0 12mm 0}
        .patient-info{border-top:2px solid #000;margin-bottom:14px}
        .pi-row{display:grid;grid-template-columns:1fr 1fr;gap:0 20px;padding:6px 0;font-size:15px}
        .pi-row:last-child{border-bottom:2px solid #000}
        .pi-row b{display:inline-block;min-width:112px}
        .test-block{page-break-inside:avoid;margin-bottom:18px}
        .test-title{font-weight:700;font-size:13px;margin:2px 0 6px}
        table.result-table{width:100%;border-collapse:collapse;table-layout:auto}
        .result-table thead th{background:#5b8fc9;color:#fff;padding:7px 8px;text-align:left;font-size:12px;font-weight:700}
        .result-table tbody td{padding:6px 8px;text-align:left;vertical-align:top;word-break:break-word;font-size:12px}
        .no-col{width:34px;text-align:center}
        .muted-cell{color:#777;font-style:italic}
        .note{margin-top:8px;font-size:12px}
        .report-footer{flex:0 0 auto;display:flex;justify-content:space-between;align-items:flex-end;border-top:1px solid #333;padding:10px 12mm;font-size:11px;color:#333}
        .footer-meta div{margin:2px 0}
        .signature-block{text-align:center}
        .signature-block img{display:block;max-height:46px;max-width:160px;margin:0 auto 4px}
        .signature-block .sig-label{font-size:11px;color:#333;margin-bottom:2px}
        .signature-block .sig-name{font-weight:700;font-size:12px;border-top:1px solid #333;padding-top:3px;margin-top:2px}
        .signature-block .sig-qual{font-size:11px;color:#333}
      </style>
    </head>
    <body>
      <div class="page">
        <div class="header">
          ${hospital.labHeader?`<img src="${assetUrl(hospital.labHeader)}">`:''}
        </div>
        <div class="content">
          <div class="patient-info">
            <div class="pi-row"><span><b>Name:</b> ${escapeHtml(patient.name||'')}</span><span><b>Age/Gender:</b> ${escapeHtml(patient.age??'')} Years/${escapeHtml(patient.gender||'')}</span></div>
            <div class="pi-row"><span><b>Referred By:</b> N.A</span><span><b>Client Name:</b> N.A</span></div>
            <div class="pi-row"><span><b>Collection Date:</b> ${formatDateTime(order?.createdAtUtc)}</span><span><b>Report Release Date:</b> ${formatDateTime(reportDate)}</span></div>
          </div>
          ${blocksHtml}
        </div>
        <div class="report-footer">
          <div class="footer-meta">
            <div><b>Order No:</b> ${escapeHtml(order?.orderNumber||'')}</div>
            <div><b>Patient:</b> ${escapeHtml(patient.name||'')} · ${escapeHtml(patient.patientCode||'')}</div>
          </div>
          <div class="signature-block">
            <div class="sig-label">Authorized Signatory</div>
            ${hospital.labSignature?`<img src="${assetUrl(hospital.labSignature)}">`:''}
            <div class="sig-name">${escapeHtml(hospital.labSignatoryName||'')}</div>
            <div class="sig-qual">${escapeHtml(hospital.labSignatoryQualification||'')}</div>
          </div>
        </div>
      </div>
      <script>
        window.onload=()=>window.print();
        window.onafterprint=()=>window.parent.postMessage('lab-print','*');
      <\/script>
    </body>
  </html>`;
}

function openPrintFrame(html){
  const f=document.createElement('iframe');
  f.style.position='fixed';
  f.style.width='0';
  f.style.height='0';
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
    blocksHtml:buildTestBlockHtml(data)
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
    blocksHtml:results.map(buildTestBlockHtml).join('')
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
<div class="toolbar"><button v-if="can('LAB','add')" @click="newOrder">+ Add Test Patient</button></div>
<div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all patient lab order fields..." /></div>
<table class="table"><tr><th class="sortable" @click="sortBy('orderNumber')">Order <span class="sort-indicator">{{sortIndicator('orderNumber')}}</span></th><th class="sortable" @click="sortBy('patientName')">Patient <span class="sort-indicator">{{sortIndicator('patientName')}}</span></th><th class="sortable" @click="sortBy('createdAtUtc')">Date <span class="sort-indicator">{{sortIndicator('createdAtUtc')}}</span></th><th class="sortable" @click="sortBy('testCount')">Tests <span class="sort-indicator">{{sortIndicator('testCount')}}</span></th><th class="sortable" @click="sortBy('status')">Status <span class="sort-indicator">{{sortIndicator('status')}}</span></th><th></th></tr><tr v-for="x in pagedOrders" :key="x.id"><td>{{x.orderNumber}}</td><td>{{x.patientName}}<div class="muted">{{x.patientCode}}</div></td><td>{{new Date(x.createdAtUtc).toLocaleString()}}</td><td>{{x.testCount}}</td><td>{{x.status}}</td><td class="actions"><button @click="openOrder(x)">View / Update Results</button><button @click="printOrderResults(x)">Print</button></td></tr></table>
<Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page=$event" />
<div v-if="showOrder" class="modal-bg"><div class="modal modal-xl"><button class="modal-close-x" @click="showOrder=false">×</button><h2>Add Tests for Patient</h2><label>Patient *<select v-model.number="order.patientId"><option :value="null">Select Patient</option><option v-for="p in patients" :key="p.id" :value="p.id">{{p.name}} · {{p.patientCode}}</option></select></label><table class="table" style="margin-top:16px"><tr><th>Test</th><th>Price</th><th>Approved Discount</th><th>Discount</th><th>After Discount</th><th></th></tr><tr v-for="(l,i) in order.tests" :key="i"><td><select v-model.number="l.labTestId"><option :value="null">Select Test</option><option v-for="t in tests" :key="t.id" :value="t.id">{{t.name}}</option></select></td><td>₹{{linePrice(l).toFixed(2)}}</td><td><select v-model.number="l.discountTypeId"><option :value="null">No Discount</option><option v-for="d in discounts" :key="d.id" :value="d.id">{{d.name}} · {{d.discountMode==='Percent'?d.value+'%':'₹'+Number(d.value).toFixed(2)}}</option></select></td><td>₹{{lineDisc(l).toFixed(2)}}</td><td>₹{{(linePrice(l)-lineDisc(l)).toFixed(2)}}</td><td><button class="danger-btn" @click="removeTestLine(i)">×</button></td></tr></table><button @click="addTestLine">+ Add Test</button><div class="bill-total-box"><div><span>Before Discount</span><b>₹{{beforeTotal().toFixed(2)}}</b></div><div class="grand"><span>After Discount</span><b>₹{{afterTotal().toFixed(2)}}</b></div><div><span>Round Off (-9 to 9)</span><input v-model.number="order.roundOff" type="number" min="-9" max="9" step="1" style="width:80px;display:inline-block"></div><div class="grand"><span>Final Amount</span><b>₹{{finalTotal().toFixed(2)}}</b></div></div><div class="modal-actions"><button @click="saveOrder">Create Tests + Bill</button><button class="secondary" @click="showOrder=false">Cancel</button></div></div></div>
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
              <input
                v-model="row.values[column.id]"
                :placeholder="column.name"
              >
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="resultData.testNote" class="note-box">
      <b>Note:</b> {{resultData.testNote}}
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