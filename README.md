# HL7 Instrument Bridge (.NET 8 Worker Service)

A production-oriented, lightweight Windows-native bridge for unidirectional instrument-to-LIS integration.

## OBX strategy decision

This implementation uses **two OBX segments per target**:
1. Quantitative copies value (`OBX-2 = NM` by default).
2. Qualitative interpretation (`POS`/`NEG`) as coded value (`OBX-2 = CWE`).

Why this is better for production:
- Keeps numeric trendable values separate from interpretation flags.
- Supports LIS rules that evaluate either concentration or interpretation independently.
- Prevents overloading one OBX with mixed semantics.
- Aligns with common ORU patterns used by analyzer integrations.

## Architecture

- `InstrumentBridgeWorker` — polling orchestration and file lifecycle.
- `FileStateManager` — incoming/processing/sent/error transitions, HL7 archive.
- `ClosedXmlTargetParser` — robust workbook parsing and validation.
- `ResultMapper` — one ORU per Sample.
- `Hl7MessageBuilder` — ORU^R01 segment construction.
- `MllpClient` — TCP + MLLP framing and ACK handling.
- `FileFingerprintDuplicateGuard` — SHA-256 duplicate protection.

## Folder state machine

1. `incoming` receives `.xlsx` exports.
2. Worker waits for stability (size/write-time unchanged for configured window + exclusive lock check).
3. File moved atomically to `processing`.
4. Parse + map + send each Sample ORU to LIS.
5. If all Samples ACK successfully: file moved to `sent`.
6. Any fatal failure: file moved to `error` and `.error.txt` with stack trace is emitted.
7. Optional HL7 payload copies stored in `hl7_archive`.

## Excel parsing rules

- Required sheets validated:
  - `Result`
  - `DetailResult`
  - `PositiveNegativeNumber`
  - `Target`
- `Target` row 1 is ignored as title.
- Headers are read from row 2.
- Data starts at row 3.
- Parsing stops at first row where both Sample and Target are empty.
- Missing numeric copies values are tolerated (`null`), not fatal.

## Project layout

```text
Hl7Bridge.sln
Hl7Bridge.Service/
  Hl7Bridge.Service.csproj
  Program.cs
  InstrumentBridgeWorker.cs
  appsettings.json
  Configuration/
    BridgeOptions.cs
  Core/
    Models.cs
  Excel/
    IExcelTargetParser.cs
    ClosedXmlTargetParser.cs
  Hl7/
    IHl7MessageBuilder.cs
    Hl7MessageBuilder.cs
  Infrastructure/
    IMllpClient.cs
    MllpClient.cs
  Processing/
    IResultMapper.cs
    ResultMapper.cs
    ISampleDispatchService.cs
    SampleDispatchService.cs
  State/
    IFileStateManager.cs
    FileStateManager.cs
    IDuplicateGuard.cs
    FileFingerprintDuplicateGuard.cs
```

## Example HL7 ORU^R01 (sample `3-P1`)

```hl7
MSH|^~\&|PCR_INSTRUMENT_BRIDGE|LAB_A|CENTRAL_LIS|MAIN_HOSPITAL|20260417091530||ORU^R01^ORU_R01|20260417091530123-3-P1|P|2.5.1
PID|1||SAMPLE-3-P1||3-P1
OBR|1||3-P1|INSTRUMENT_PANEL^P01^99LOCAL|20260417091530|||||||||||3-P1
OBX|1|NM|KPNEU^Klebsiella pneumoniae^99LAB|1|1500.2|copies/uL|||||F|||20260417091530
OBX|2|CWE|KPNEU^Klebsiella pneumoniae^99LAB-INT^Interpretation^99LOCAL|1|POS^POS^HL70078|||||F|||20260417091530
OBX|3|NM|ECOLI^Escherichia coli^99LAB|1|0|copies/uL|||||F|||20260417091530
OBX|4|CWE|ECOLI^Escherichia coli^99LAB-INT^Interpretation^99LOCAL|1|NEG^NEG^HL70078|||||F|||20260417091530
OBX|5|NM|INTCTRL^Internal Control^99LAB|1|342.0|copies/uL|||||F|||20260417091530
OBX|6|CWE|INTCTRL^Internal Control^99LAB-INT^Interpretation^99LOCAL|1|POS^POS^HL70078|||||F|||20260417091530
```

## Deployment on Windows

1. Install .NET 8 Runtime / Hosting Bundle.
2. Publish service:
   - `dotnet publish Hl7Bridge.Service/Hl7Bridge.Service.csproj -c Release -r win-x64 --self-contained false`
3. Create directories from `appsettings.json`.
4. Install service:
   - `sc create Hl7InstrumentBridge binPath= "C:\Path\publish\Hl7Bridge.Service.exe" start= auto`
5. Start service:
   - `sc start Hl7InstrumentBridge`
6. View logs in configured `logs` folder.

## Production hardening checklist

- Add TLS tunnel or VPN if LIS connection crosses insecure networks.
- Harden ACK parsing to enforce matching message control IDs.
- Add structured dead-letter queue/reporting for non-AA ACKs.
- Add row-level validation reports for malformed data.
- Add health heartbeat file/event log probes for monitoring.
- Add integration tests against a LIS simulator (e.g., Mirth listener).
- Lock down service account permissions to least privilege.
