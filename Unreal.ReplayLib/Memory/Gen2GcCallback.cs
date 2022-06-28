using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace Unreal.ReplayLib.Memory
{
    internal sealed class Gen2GcCallback : CriticalFinalizerObject
    {
        private readonly Func<bool> _callback0;
        private readonly Func<object, bool> _callback1;
        private GCHandle _weakTargetObj;

        private Gen2GcCallback(Func<bool> callback) => _callback0 = callback;

        private Gen2GcCallback(Func<object, bool> callback, object targetObj)
        {
            _callback1 = callback;
            _weakTargetObj = GCHandle.Alloc(targetObj, GCHandleType.Weak);
        }

        public static void Register(Func<bool> callback)
        {
            // Create a unreachable object that remembers the callback function and target object.
            _ = new Gen2GcCallback(callback);
        }

        public static void Register(Func<object, bool> callback, object targetObj)
        {
            // Create a unreachable object that remembers the callback function and target object.
            _ = new Gen2GcCallback(callback, targetObj);
        }

        ~Gen2GcCallback()
        {
            if (_weakTargetObj.IsAllocated)
            {
                // Check to see if the target object is still alive.
                var targetObj = _weakTargetObj.Target;
                if (targetObj == null)
                {
                    // The target object is dead, so this callback object is no longer needed.
                    _weakTargetObj.Free();
                    return;
                }

                // Execute the callback method.
                try
                {
                    if (!_callback1(targetObj))
                    {
                        // If the callback returns false, this callback object is no longer needed.
                        _weakTargetObj.Free();
                        return;
                    }
                }
                catch
                {
                    // Ensure that we still get a chance to resurrect this object, even if the callback throws an exception.
                }
            }
            else
            {
                // Execute the callback method.
                try
                {
                    if (!_callback0())
                    {
                        // If the callback returns false, this callback object is no longer needed.
                        return;
                    }
                }
                catch
                {
                    // Ensure that we still get a chance to resurrect this object, even if the callback throws an exception.
                }
            }

            // Resurrect ourselves by re-registering for finalization.
            GC.ReRegisterForFinalize(this);
        }
    }
}