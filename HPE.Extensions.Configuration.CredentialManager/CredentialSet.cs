﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HPE.Extensions.Configuration.CredentialManager
{
    public class CredentialSet : List<Credential>, IDisposable
    {
        bool _disposed;

        public CredentialSet()
        {
        }

        public CredentialSet(string targetPrefix)
            : this()
        {
            if (string.IsNullOrEmpty(targetPrefix))
            {
                TargetPrefix = null;
            }
            else
            {
                if (!targetPrefix.EndsWith("*"))
                {
                    targetPrefix = $"{targetPrefix}*";
                }

                TargetPrefix = targetPrefix;
            }
        }

        public string TargetPrefix { get; set; }

        public void Dispose()
        {
            Dispose(true);

            // Prevent GC Collection since we have already disposed of this object
            GC.SuppressFinalize(this);
        }

        ~CredentialSet()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (Count > 0)
                    {
                        ForEach(cred => cred.Dispose());
                    }
                }
            }
            _disposed = true;
        }

        public CredentialSet Load()
        {
            LoadInternal();
            return this;
        }

        private void LoadInternal()
        {
            IntPtr pCredentials = IntPtr.Zero;
            bool result = NativeMethods.CredEnumerateW(TargetPrefix, 0, out uint count, out pCredentials);
            if (!result)
            {
                Trace.WriteLine($"Win32Exception: {new Win32Exception(Marshal.GetLastWin32Error())}");
                return;
            }

            // Read in all of the pointers first
            IntPtr[] ptrCredList = new IntPtr[count];
            for (int i = 0; i < count; i++)
            {
                ptrCredList[i] = Marshal.ReadIntPtr(pCredentials, IntPtr.Size * i);
            }

            // Now let's go through all of the pointers in the list
            // and create our Credential object(s)
            List<NativeMethods.CriticalCredentialHandle> credentialHandles =
                ptrCredList.Select(ptrCred => new NativeMethods.CriticalCredentialHandle(ptrCred)).ToList();

            IEnumerable<Credential> existingCredentials = credentialHandles
                .Select(handle => handle.GetCredential())
                .Select(nativeCredential =>
                {
                    Credential credential = new Credential();
                    credential.LoadInternal(nativeCredential);
                    return credential;
                });
            AddRange(existingCredentials);

            // The individual credentials should not be free'd
            credentialHandles.ForEach(handle => handle.SetHandleAsInvalid());

            // Clean up memory to the Enumeration pointer
            NativeMethods.CredFree(pCredentials);
        }
    }

}
