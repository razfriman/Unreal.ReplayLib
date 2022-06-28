namespace Unreal.ReplayLib.Models.Enums;

public enum EngineNetworkVersionHistory
{
    HistoryInitial = 1,

    HistoryReplayBackwardsCompat =
        2, // Bump version to get rid of older replays before backwards compat was turned on officially

    HistoryMaxActorChannelsCustomization =
        3, // Bump version because serialization of the actor channels changed

    HistoryRepcmdChecksumRemovePrintf =
        4, // Bump version since the way FRepLayoutCmd::CompatibleChecksum was calculated changed due to an optimization

    HistoryNewActorOverrideLevel =
        5, // Bump version since a level reference was added to the new actor information
    HistoryChannelNames = 6, // Bump version since channel type is now an fname

    HistoryChannelCloseReason =
        7, // Bump version to serialize a channel close reason in bunches instead of bDormant
    HistoryAcksIncludedInHeader = 8, // Bump version since acks are now sent as part of the header
    HistoryNetexportSerialization = 9, // Bump version due to serialization change to FNetFieldExport
    HistoryNetexportSerializeFix = 10, // Bump version to fix net field export name serialization

    HistoryFastArrayDeltaStruct =
        11, // Bump version to allow fast array serialization, delta struct serialization.
    HistoryFixEnumSerialization = 12, // Bump version to fix enum net serialization issues.

    HistoryOptionallyQuantizeSpawnInfo =
        13, // Bump version to conditionally disable quantization for Scale, Location, and Velocity when spawning network actors.

    HistoryJitterInHeader =
        14, // Bump version since we added jitter clock time to packet headers and removed remote saturation
    HistoryClassnetcacheFullname = 15, // Bump version to use full paths in GetNetFieldExportGroupForClassNetCache

    //https://github.com/EpicGames/UnrealEngine/blob/2bf1a5b83a7076a0fd275887b373f8ec9e99d431/Engine/Source/Runtime/Engine/Private/NetConnection.cpp#L2349
    //Used for fast forwarding. No need to implement
    HistoryReplayDormancy = 16, // Bump version to support dormancy properly in replays

    HistoryEnumSerializationCompat =
        17, // Bump version to include enum bits required for serialization into compat checksums, as well as unify enum and byte property enum serialization (TODO)

    HistorySubobjectOuterChain = 18, // Bump version to support subobject outer chains matching on client and server
    HistoryHitresultInstancehandle = 19,
    HistoryInterfacePropertySerialization = 20, // Bump version to support net serialization of FInterfaceProperty

    HistoryMontagePlayInstIdSerialization =
        21, // Bump version to support net serialization of FGameplayAbilityRepAnimMontage, addition of PlayInstanceId and removal of bForcePlayBit

    // Bump version to support FHitResult change of Actor to HitObjectHandle. This change was made in CL 14369221 but a net version wasn't added at the time.
    HISTORY_SERIALIZE_DOUBLE_VECTORS_AS_DOUBLES =
        22, // Bump version to support net serialization of double vector types
    HISTORY_PACKED_VECTOR_LWC_SUPPORT = 23, // Bump version to support quantized LWC FVector net serialization
    HISTORY_PAWN_REMOTEVIEWPITCH = 24, // Bump version to support serialization changes to RemoteViewPitch

    HISTORY_REPMOVE_SERVERFRAME_AND_HANDLE =
        25, // Bump version to support serialization changes to RepMove so we can get the serverframe and physics handle associated with the object

    HISTORY_21_AND_VIEWPITCH_ONLY_DO_NOT_USE =
        26, // Bump version to support up to history 21 + HISTORY_PAWN_REMOTEVIEWPITCH.  DO NOT USE!!!

    HISTORY_PLACEHOLDER =
        27, // Bump version to a placeholder.  This version is the same as HISTORY_REPMOVE_SERVERFRAME_AND_HANDLE

    // New history items go above here.
    HistoryEnginenetversionPlusOne,
    HistoryEnginenetversionLatest = HistoryEnginenetversionPlusOne - 1
}