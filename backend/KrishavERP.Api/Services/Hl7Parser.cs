namespace KrishavERP.Api.Services;

// Minimal HL7 v2.3.1 segment/field parser - only what the BM500 LIS protocol
// (see "BM500 LIS communication protocol.pdf") actually needs: splitting a
// message into segments, each segment into 1-based fields (matching the SN
// column in the protocol's field tables), and components/sub-components
// within a field. Not a general-purpose HL7 library.
public class Hl7Segment
{
    public string Name { get; init; } = "";

    // 1-based: Fields[1] is field 1, matching the SN column in the protocol
    // spec's tables directly. Fields[0] is unused/blank.
    public List<string> Fields { get; init; } = new() { "" };

    public string Field(int sn) => sn > 0 && sn < Fields.Count ? Fields[sn] : "";

    // Components within a field are separated by '^' (1-based, Component(field,1) is the first component).
    public static string Component(string field, int index)
    {
        if (string.IsNullOrEmpty(field)) return "";
        var parts = field.Split('^');
        return index > 0 && index <= parts.Length ? parts[index - 1] : "";
    }
}

public class Hl7Message
{
    public List<Hl7Segment> Segments { get; } = new();

    public Hl7Segment? Get(string name) => Segments.FirstOrDefault(s => s.Name == name);
    public IEnumerable<Hl7Segment> GetAll(string name) => Segments.Where(s => s.Name == name);
}

public static class Hl7Parser
{
    // Splits a raw HL7 message (segments separated by <CR>, tolerating stray
    // <LF>/<CR><LF> from senders that don't follow the spec exactly) into
    // segments and 1-based fields. MSH is special-cased per the spec: the
    // character right after "MSH" is field 1 (the field separator itself),
    // and everything else in the segment is split using that same character.
    public static Hl7Message Parse(string raw)
    {
        var message = new Hl7Message();
        var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var fieldSep = '|';

        foreach (var line in lines)
        {
            if (line.Length < 3) continue;
            var name = line.Substring(0, 3);
            var fields = new List<string> { "" };

            if (name == "MSH")
            {
                fieldSep = line.Length > 3 ? line[3] : '|';
                fields.Add(fieldSep.ToString());
                var rest = line.Length > 4 ? line.Substring(4) : "";
                fields.AddRange(rest.Split(fieldSep));
            }
            else
            {
                var rest = line.Length > 4 ? line.Substring(4) : "";
                fields.AddRange(rest.Split(fieldSep));
            }

            message.Segments.Add(new Hl7Segment { Name = name, Fields = fields });
        }

        return message;
    }

    // Builds an MSH+MSA acknowledgement message for a received message,
    // per section 3.3.1 "Sample response message" of the protocol doc.
    public static string BuildAck(Hl7Message received, string ackCode = "AA")
    {
        var msh = received.Get("MSH");
        var messageType = msh?.Field(9) ?? "";
        var eventCode = Hl7Segment.Component(messageType, 2);
        var processingId = msh?.Field(11) ?? "P";
        var controlId = msh?.Field(10) ?? "";
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        var ackType = string.IsNullOrEmpty(eventCode) ? "ACK^R01" : $"ACK^{eventCode}";
        var ackMsh = $"MSH|^~\\&|KrishavERP|KrishavERP|||{timestamp}||{ackType}|{Guid.NewGuid():N}|{processingId}|2.3.1||||||UNICODE";
        var msa = $"MSA|{ackCode}|{controlId}";

        return ackMsh + "\r" + msa + "\r";
    }

    // MLLP framing: <SB=0x0B> data <EB=0x1C><CR=0x0D>.
    public static byte[] FrameMllp(string message)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);
        var framed = new byte[bytes.Length + 3];
        framed[0] = 0x0B;
        Array.Copy(bytes, 0, framed, 1, bytes.Length);
        framed[^2] = 0x1C;
        framed[^1] = 0x0D;
        return framed;
    }
}
