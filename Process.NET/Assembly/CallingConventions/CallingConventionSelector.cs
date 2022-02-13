using System;
using Process.NET.Utilities;

namespace Process.NET.Assembly.CallingConventions
{
    /// <summary>
    ///     Static class providing calling convention instances.
    /// </summary>
    public static class CallingConventionSelector
    {
        /// <summary>
        ///     Gets a calling convention object according the given type.
        /// </summary>
        /// <param name="callingConvention">The type of calling convention to get.</param>
        /// <returns>The return value is a singleton of a <see cref="ICallingConvention" /> child.</returns>
        public static ICallingConvention Get(Native.Types.CallingConventions callingConvention)
        {
            return callingConvention switch
            {
                Native.Types.CallingConventions.Cdecl => Singleton<CdeclCallingConvention>.Instance,
                Native.Types.CallingConventions.Stdcall => Singleton<StdcallCallingConvention>.Instance,
                Native.Types.CallingConventions.Fastcall => Singleton<FastcallCallingConvention>.Instance,
                Native.Types.CallingConventions.Thiscall => Singleton<ThiscallCallingConvention>.Instance,
                _ => throw new ApplicationException("Unsupported calling convention."),
            };
        }
    }
}