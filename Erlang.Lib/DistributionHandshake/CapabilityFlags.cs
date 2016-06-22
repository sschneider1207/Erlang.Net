using System;

namespace Erlang.Lib.DistributionHandshake
{
    [Flags]
    internal enum CapabilityFlags
    {
        Published = 1,
        AtomCache = 2,
        ExtendedReferences = 4,
        DistMonitor = 8,
        FunTags = 0x10,
        DistMonitorName = 0x20,
        HiddenAtomCache = 0x40,
        NewFunTags = 0x80,
        ExtendedPidsPorts = 0x100,
        ExportPtrTag = 0x200,
        BitBinaries = 0x400,
        NewFloats = 0x800,
        UnicodeIO = 0x1000,
        DistHDRAtomCache = 0x2000,
        SmallAtomTags = 0x4000,
        UTF8Atoms = 0x10000,
        MapTag = 0x20000
    }
}
