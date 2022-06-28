using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Exceptions;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite;

public class FortniteReplayReader : ReplayReader<FortniteReplay, FortniteReplayState>
{
    private readonly FortniteReplayEventParser _fortniteReplayEventParser;

    public FortniteReplayReader(ILogger logger) : base(logger)
    {
        _fortniteReplayEventParser = new FortniteReplayEventParser(logger);
    }

    public override void OnParseReplayEvent(ReplayEvent replayEvent, UnrealBinaryReader archive)
    {
        try
        {
            archive.Seek(replayEvent.Position);
            var decryptedReader = Decrypt(archive, replayEvent.SizeInBytes);

            switch (replayEvent.Group)
            {
                case "AdditionGFPEventGroup" when replayEvent.Metadata == "AdditionGFPEvent":
                    var unk = decryptedReader.ReadBytes(16);
                    break;
                case "playerElim" when replayEvent.Metadata == "versionedEvent":
                    Replay.Eliminations.Add(_fortniteReplayEventParser.ParseElimination(decryptedReader, replayEvent));
                    break;
                case "PlayerStateEncryptionKey" when replayEvent.Metadata == "PlayerStateEncryptionKey":
                    Replay.PlayerStateEncryptionKeyEvent =
                        _fortniteReplayEventParser.ParseEncryptionKeyEvent(decryptedReader, replayEvent);
                    break;
                case "AthenaReplayBrowserEvents" when replayEvent.Metadata == "AthenaMatchStats":
                    Replay.StatsEvent = _fortniteReplayEventParser.ParseMatchStats(decryptedReader, replayEvent);
                    break;
                case "AthenaReplayBrowserEvents" when replayEvent.Metadata == "AthenaMatchTeamStats":
                    Replay.TeamStatsEvent = _fortniteReplayEventParser.ParseTeamStats(decryptedReader, replayEvent);
                    break;
                case "Timecode" when replayEvent.Metadata == "TimecodeVersionedMeta":
                {
                    Replay.TimecodeEvent = _fortniteReplayEventParser.ParseTimecode(decryptedReader, replayEvent);
                    break;
                }
                case "ActorsPosition" when replayEvent.Metadata == "ActorsVersionedMeta":
                {
                    Replay.ActorPositionEvents.Add(
                        _fortniteReplayEventParser.ParseActorPositions(decryptedReader, replayEvent));
                    break;
                }
                case "CharacterSample" when replayEvent.Metadata == "CharacterSampleMeta":
                {
                    Replay.CharacterSampleEvents.Add(
                        _fortniteReplayEventParser.ParseCharacterSamples(decryptedReader, replayEvent));
                    break;
                }
                case "ZoneUpdate" when replayEvent.Metadata == "ZoneVersionedMeta":
                {
                    Replay.ZoneUpdateEvents.Add(
                        _fortniteReplayEventParser.ParseZoneUpdate(decryptedReader, replayEvent));
                    break;
                }
                case "sessionG0" when replayEvent.Metadata == "sessionM":
                {
                    // Unknown - encrypted?
                    break;
                }
                default:
                {
                    Logger?.LogWarning(
                        "Unknown event: {Group} - {Metadata} of size {SizeInBytes}", replayEvent.Group, replayEvent.Metadata, replayEvent.SizeInBytes);
                    break;
                }
            }

            if (decryptedReader.Available > 0)
            {
                Logger?.LogWarning(
                    "Event was not fully parsed: {Group} - {Metadata} with available bytes {Available}", replayEvent.Group, replayEvent.Metadata, decryptedReader.Available);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error while parsing event {Group} - {Metadata} at timestamp {Timestamp}",
                replayEvent.Group, replayEvent.Metadata, replayEvent.StartTime);
            throw new ReplayException(
                $"Error while parsing event", ex);
        }
    }
}