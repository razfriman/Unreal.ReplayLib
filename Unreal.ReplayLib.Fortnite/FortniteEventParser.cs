using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Fortnite.Models;
using Unreal.ReplayLib.Fortnite.Models.Enums;
using Unreal.ReplayLib.Fortnite.Models.Events;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite;

public class FortniteReplayEventParser
{
    private const int HalfMapSize = 131328;
    private const int CurrentEventVersion = 9;
    private readonly ILogger _logger;
    public FortniteReplayEventParser(ILogger logger) => _logger = logger;

    public PlayerEliminationEvent ParseElimination(UnrealBinaryReader archive, ReplayEvent replayEvent)
    {
        var elim = new PlayerEliminationEvent
        {
            ReplayEvent = replayEvent
        };

        var eventVersion = archive.ReadInt32();

        var eventType = archive.ReadByte();

        if (eventVersion == CurrentEventVersion && eventType == 4)
        {
            var eventLocation = new FTransform();
            eventLocation.Serialize(archive);

            var instigatorLocation = new FTransform();
            instigatorLocation.Serialize(archive);

            elim.EliminatedInfo = new PlayerEliminationInfo
            {
                Location = eventLocation.Scale3D,
            };
            elim.EliminatorInfo = new PlayerEliminationInfo
            {
                Location = instigatorLocation.Scale3D
            };

            ParsePlayer(archive, elim.EliminatedInfo);
            ParsePlayer(archive, elim.EliminatorInfo);
        }
        else
        {
            _logger?.LogWarning("Unknown Elimination Event Version: {EventVersion} Type: {EventType}", eventVersion,
                eventType);
        }

        elim.GunType = archive.ReadByteAsEnum<EDeathCause>();
        elim.Knocked = archive.ReadBoolean();
        var unk1 = archive.ReadByte();
        var unk2 = archive.ReadByte();
        var unk3 = archive.ReadByte();
        return elim;
    }

    public EncryptionKeyEvent ParseEncryptionKeyEvent(UnrealBinaryReader archive, ReplayEvent replayEvent) =>
        new()
        {
            ReplayEvent = replayEvent,
            Key = archive.ReadBytesToString(32)
        };

    public TeamStatsEvent ParseTeamStats(UnrealBinaryReader archive, ReplayEvent replayEvent)
    {
        var version = archive.ReadInt32();

        if (version != 0)
        {
            _logger?.LogWarning(
                "Unknown event version. Group: {Group} Metadata: {Metadata} Version: {Version}", replayEvent.Group,
                replayEvent.Metadata, version);
        }

        return new()
        {
            ReplayEvent = replayEvent,
            Position = archive.ReadUInt32(),
            TotalPlayers = archive.ReadUInt32()
        };
    }


    public StatsEvent ParseMatchStats(UnrealBinaryReader archive, ReplayEvent replayEvent)
    {
        var version = archive.ReadInt32();

        if (version != 0)
        {
            _logger?.LogWarning(
                "Unknown event version. Group: {Group} Metadata: {Metadata} Version: {Version}", replayEvent.Group,
                replayEvent.Metadata, version);
        }

        return new()
        {
            ReplayEvent = replayEvent,
            Accuracy = archive.ReadSingle(),
            Assists = archive.ReadUInt32(),
            Eliminations = archive.ReadUInt32(),
            WeaponDamage = archive.ReadUInt32(),
            OtherDamage = archive.ReadUInt32(),
            Revives = archive.ReadUInt32(),
            DamageTaken = archive.ReadUInt32(),
            DamageToStructures = archive.ReadUInt32(),
            MaterialsGathered = archive.ReadUInt32(),
            MaterialsUsed = archive.ReadUInt32(),
            TotalTraveled = archive.ReadUInt32()
        };
    }

    public void ParsePlayer(UnrealBinaryReader archive, PlayerEliminationInfo info)
    {
        info.PlayerType = archive.ReadByteAsEnum<PlayerTypes>();

        switch (info.PlayerType)
        {
            case PlayerTypes.Bot:
                break;
            case PlayerTypes.NamedBot:
                info.Id = archive.ReadFString();
                break;
            case PlayerTypes.Player:
                info.Id = archive.ReadGuid(archive.ReadByte())?.ToLower();
                break;
            default:
                _logger?.LogWarning("Unknown player type: {PlayerType}", info.PlayerType);
                break;
        }
    }

    public TimecodeEvent? ParseTimecode(UnrealBinaryReader archive, ReplayEvent replayEvent)
    {
        var version = archive.ReadInt32();

        if (version != CurrentEventVersion)
        {
            _logger?.LogWarning(
                "Unknown event version. Group: {Group} Metadata: {Metadata} Version: {Version}", replayEvent.Group,
                replayEvent.Metadata, version);
        }

        var date = archive.ReadDate();

        return new TimecodeEvent
        {
            ReplayEvent = replayEvent,
            Timecode = date
        };
    }

    public ActorPositionsEvent ParseActorPositions(UnrealBinaryReader archive, ReplayEvent replayEvent)
    {
        var version = archive.ReadInt32();

        if (version != CurrentEventVersion)
        {
            _logger?.LogWarning(
                "Unknown event version. Group: {Group} Metadata: {Metadata} Version: {Version}", replayEvent.Group,
                replayEvent.Metadata, version);
        }

        var chestPositions = archive.ReadArray(archive.ReadQuantizedVector).ToList();
        //         var size = decryptedReader.ReadInt32();
        //         var x = decryptedReader.ReadInt32() - (HalfMapSize >> (16 - size));
        //         var y = decryptedReader.ReadInt32() - (HalfMapSize >> (16 - size));
        //         var z = decryptedReader.ReadInt32() - (HalfMapSize >> (16 - size));
        //         State.ChestPositions.Add(new FVector(x, y, z));

        return new ActorPositionsEvent
        {
            ReplayEvent = replayEvent,
            ChestPositions = chestPositions
        };
    }

    public CharacterSampleEvent ParseCharacterSamples(UnrealBinaryReader archive, ReplayEvent replayEvent)
    {
        var version = archive.ReadInt32();

        if (version != CurrentEventVersion)
        {
            _logger?.LogWarning(
                "Unknown event version. Group: {Group} Metadata: {Metadata} Version: {Version}", replayEvent.Group,
                replayEvent.Metadata, version);
        }

        var result = new CharacterSampleEvent();

        var count = archive.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            result.Samples.Add(new CharacterSample()
            {
                EpicId = archive.ReadFString(),
                MovementEvents = archive.ReadArray(() =>
                    new MovementEvent
                    {
                        //             var size = decryptedReader.ReadInt32();
                        //             var x = decryptedReader.ReadInt32() - (HalfMapSize >> (16 - size));
                        //             var y = decryptedReader.ReadInt32() - (HalfMapSize >> (16 - size));
                        //             var z = decryptedReader.ReadInt32() - (HalfMapSize >> (16 - size));
                        Position = archive.ReadQuantizedVector(),
                        MovementStyle = archive.ReadByteAsEnum<EFortMovementStyle>(),
                        DeltaGameTime = archive.ReadUInt16()
                    }).ToList()
            });
        }

        return result;
    }

    public ZoneUpdateEvent ParseZoneUpdate(UnrealBinaryReader archive, ReplayEvent replayEvent)
    {
        var version = archive.ReadInt32();

        if (version != CurrentEventVersion)
        {
            _logger?.LogWarning(
                "Unknown event version. Group: {Group} Metadata: {Metadata} Version: {Version}", replayEvent.Group,
                replayEvent.Metadata, version);
        }

        var position = new FVector();
        position.Serialize(archive);
        var radius = archive.ReadSingle();
        return new ZoneUpdateEvent
        {
            ReplayEvent = replayEvent,
            Position = position,
            Radius = radius
        };
    }
}