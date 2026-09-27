using System.Net.Sockets;
using System.Text;
using KrishavERP.Api;

namespace KrishavERP.Api.Services;

// TCP/MLLP listener implementing the BM500 LIS communication protocol
// ("BM500 LIS communication protocol.pdf"): the analyzer's own software acts
// as the client and connects to us (the "LIS server") to push ORU^R01
// messages carrying CBC results and histogram/scattergram images. We only
// implement the receive-and-acknowledge half (section 3.1.2's ORU^R01/ACK^R01
// pair) - the optional bi-directional work-order query (ORM/ORR) is not
// implemented, so an unrecognized message type is just ACKed and ignored
// rather than erroring the connection.
//
// This is intentionally a self-contained new module: it only ever writes to
// the new CbcAnalyzer* tables and never touches LabOrder/LabOrderTest/
// LabResult, so it cannot affect the existing manually-entered Lab workflow.
public class CbcAnalyzerListenerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CbcAnalyzerListenerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public CbcAnalyzerListenerService(
        IServiceScopeFactory scopeFactory,
        ILogger<CbcAnalyzerListenerService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("CbcAnalyzer:Enabled", true))
        {
            _logger.LogInformation("CBC analyzer listener disabled via configuration.");
            return;
        }

        var port = _configuration.GetValue("CbcAnalyzer:Port", 6000);
        TcpListener listener;

        try
        {
            listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CBC analyzer listener failed to bind port {Port}.", port);
            return;
        }

        _logger.LogInformation("CBC analyzer (BM500 LIS) listener started on port {Port}.", port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = HandleClientAsync(client, stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
    {
        using var _ = client;
        client.NoDelay = true;
        var buffer = new List<byte>();
        var readBuffer = new byte[8192];

        try
        {
            using var stream = client.GetStream();

            while (!stoppingToken.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await stream.ReadAsync(readBuffer, stoppingToken);
                }
                catch (IOException)
                {
                    break;
                }

                if (read == 0) break; // client closed the connection

                buffer.AddRange(readBuffer.Take(read));

                // MLLP framing: <VT=0x0B> ... <FS=0x1C><CR=0x0D>. Drain every
                // complete frame currently in the buffer before waiting for more
                // bytes, since several messages can arrive back-to-back.
                int frameStart;
                while ((frameStart = buffer.IndexOf(0x0B)) >= 0)
                {
                    var frameEnd = IndexOfFrameEnd(buffer, frameStart);
                    if (frameEnd < 0) break;

                    var payload = buffer.GetRange(frameStart + 1, frameEnd - frameStart - 1).ToArray();
                    buffer.RemoveRange(0, frameEnd + 2); // drop through the trailing <CR>

                    var raw = Encoding.UTF8.GetString(payload);
                    await ProcessMessageAsync(raw, stream, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CBC analyzer connection ended unexpectedly.");
        }
    }

    // Finds the index of <FS> that is immediately followed by <CR>, starting
    // the search after frameStart.
    private static int IndexOfFrameEnd(List<byte> buffer, int frameStart)
    {
        for (var i = frameStart + 1; i < buffer.Count - 1; i++)
        {
            if (buffer[i] == 0x1C && buffer[i + 1] == 0x0D) return i;
        }
        return -1;
    }

    private async Task ProcessMessageAsync(string raw, NetworkStream stream, CancellationToken stoppingToken)
    {
        Hl7Message message;
        try
        {
            message = Hl7Parser.Parse(raw);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse an incoming HL7 message; ACKing as an error and discarding.");
            await SendAckAsync(new Hl7Message(), stream, "AE", stoppingToken);
            return;
        }

        var msh = message.Get("MSH");
        var messageType = msh?.Field(9) ?? "";

        try
        {
            if (messageType.StartsWith("ORU", StringComparison.OrdinalIgnoreCase))
            {
                await SaveOruMessageAsync(message, raw);
            }
            // ORM/ORR (bi-directional work-order query) is not implemented -
            // every other message type is simply acknowledged so the
            // analyzer doesn't retry indefinitely.

            await SendAckAsync(message, stream, "AA", stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save an incoming CBC analyzer message.");
            await SendAckAsync(message, stream, "AE", stoppingToken);
        }
    }

    private static async Task SendAckAsync(Hl7Message received, NetworkStream stream, string ackCode, CancellationToken stoppingToken)
    {
        var ack = Hl7Parser.BuildAck(received, ackCode);
        var framed = Hl7Parser.FrameMllp(ack);
        await stream.WriteAsync(framed, stoppingToken);
    }

    private async Task SaveOruMessageAsync(Hl7Message message, string raw)
    {
        var msh = message.Get("MSH");
        var pid = message.Get("PID");
        var pv1 = message.Get("PV1");
        var obrSegments = message.GetAll("OBR").ToList();
        if (obrSegments.Count == 0) return;

        var messageControlId = msh?.Field(10) ?? "";
        var processingId = string.IsNullOrWhiteSpace(msh?.Field(11)) ? "P" : msh!.Field(11);
        var patientIdentifier = Hl7Segment.Component(pid?.Field(3) ?? "", 1);
        var patientFamilyName = Hl7Segment.Component(pid?.Field(5) ?? "", 1);
        var patientGivenName = Hl7Segment.Component(pid?.Field(5) ?? "", 2);
        var patientName = string.Join(" ", new[] { patientFamilyName, patientGivenName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var gender = pid?.Field(8);
        var patientClass = pv1?.Field(2);
        var patientLocation = pv1?.Field(3);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Segments between one OBR and the next (or end of message) belong to that OBR.
        for (var obrIndex = 0; obrIndex < obrSegments.Count; obrIndex++)
        {
            var obr = obrSegments[obrIndex];
            var obrPosition = message.Segments.IndexOf(obr);
            var nextObrPosition = obrIndex + 1 < obrSegments.Count
                ? message.Segments.IndexOf(obrSegments[obrIndex + 1])
                : message.Segments.Count;
            var obxSegments = message.Segments
                .Skip(obrPosition + 1)
                .Take(nextObrPosition - obrPosition - 1)
                .Where(s => s.Name == "OBX")
                .ToList();

            var result = new CbcAnalyzerResult
            {
                MessageControlId = messageControlId,
                ProcessingId = processingId,
                SampleId = obr.Field(3),
                ResultTypeCode = Hl7Segment.Component(obr.Field(4), 1),
                ResultTypeName = Hl7Segment.Component(obr.Field(4), 2),
                PatientIdentifier = patientIdentifier,
                PatientName = patientName,
                Gender = string.IsNullOrWhiteSpace(gender) ? null : gender,
                PatientClass = string.IsNullOrWhiteSpace(patientClass) ? null : patientClass,
                PatientLocation = string.IsNullOrWhiteSpace(patientLocation) ? null : patientLocation,
                Tester = NullIfEmpty(obr.Field(10)),
                Interpreter = NullIfEmpty(obr.Field(32)),
                RequestedAtUtc = ParseHl7Timestamp(obr.Field(6)),
                ObservationAtUtc = ParseHl7Timestamp(obr.Field(7)),
                SpecimenReceivedAtUtc = ParseHl7Timestamp(obr.Field(14)),
                RawMessage = raw
            };

            var itemSort = 0;
            var imageSort = 0;

            foreach (var obx in obxSegments)
            {
                var valueType = obx.Field(2);
                var code = Hl7Segment.Component(obx.Field(3), 1);
                var name = Hl7Segment.Component(obx.Field(3), 2);
                var value = obx.Field(5);

                if (string.Equals(valueType, "ED", StringComparison.OrdinalIgnoreCase))
                {
                    var imagePath = TrySaveImage(value, name);
                    if (imagePath != null)
                    {
                        result.Images.Add(new CbcAnalyzerImage { Name = name, ImagePath = imagePath, SortOrder = imageSort++ });
                    }
                    continue;
                }

                // "Other data items" (section 5, table 9) get their own named
                // field instead of being listed as a generic result row.
                switch (code)
                {
                    case "02001": result.LoadingMode = value; continue;
                    case "02002": result.BloodMode = value; continue;
                    case "02003": result.TestMode = value; continue;
                    case "30525-0": result.AgeText = string.IsNullOrWhiteSpace(obx.Field(6)) ? value : $"{value} {obx.Field(6)}"; continue;
                    case "03001": result.RefGroup = value; continue;
                    case "09001": result.Remark = value; continue;
                }

                result.Items.Add(new CbcAnalyzerResultItem
                {
                    Code = code,
                    Name = name,
                    Value = NullIfEmpty(value),
                    Unit = NullIfEmpty(obx.Field(6)),
                    ReferenceRange = NullIfEmpty(obx.Field(7)),
                    AbnormalFlag = NullIfEmpty(obx.Field(8)),
                    SortOrder = itemSort++
                });
            }

            db.CbcAnalyzerResults.Add(result);
        }

        await db.SaveChangesAsync();
    }

    // OBX-5 for an ED (encapsulated data) field is
    // ^Image^<subtype e.g. BMP/PNG>^Base64^<data>, per section "Histogram
    // data transmission" - component 1 (source application) is blank.
    private string? TrySaveImage(string obxValue, string name)
    {
        // <source application> ^ <type of data> ^ <data sub type> ^ <encoding> ^ <data>
        var subType = Hl7Segment.Component(obxValue, 3);
        var encoding = Hl7Segment.Component(obxValue, 4);
        var data = Hl7Segment.Component(obxValue, 5);

        if (string.IsNullOrWhiteSpace(data) || !string.Equals(encoding, "Base64", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(data);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Could not decode Base64 image data for OBX item {Name}.", name);
            return null;
        }

        var extension = subType.ToUpperInvariant() switch
        {
            "PNG" => ".png",
            "BMP" => ".bmp",
            _ => ".bin"
        };

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "uploads", "cbc-analyzer");
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        File.WriteAllBytes(Path.Combine(directory, fileName), bytes);

        return $"/uploads/cbc-analyzer/{fileName}";
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // HL7 TS format: YYYY[MM[DD[HHMM[SS]]]] - the BM500 examples always send
    // the full YYYYMMDDHHMMSS form, but this tolerates shorter values too.
    private static DateTime? ParseHl7Timestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());

        foreach (var format in new[] { "yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMddHH", "yyyyMMdd", "yyyyMM", "yyyy" })
        {
            if (digits.Length >= format.Length &&
                DateTime.TryParseExact(digits[..format.Length], format, null,
                    System.Globalization.DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
