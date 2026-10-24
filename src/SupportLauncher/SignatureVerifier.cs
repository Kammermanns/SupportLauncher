using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace SupportLauncher
{
    /// <summary>
    /// Checks that a file has a valid Authenticode signature (WinVerifyTrust)
    /// and that the signer is one of the allowed publishers.
    /// A file that fails this check is never started.
    /// </summary>
    internal static class SignatureVerifier
    {
        /// <param name="checkRevocation">Online revocation check. Disabled only for the offline fallback.</param>
        public static bool IsTrusted(string filePath, string[] allowedPublishers, bool checkRevocation, out string details)
        {
            if (!VerifyAuthenticode(filePath, checkRevocation, out var trustResult))
            {
                details = $"Ungültige oder fehlende Signatur (WinVerifyTrust 0x{trustResult:X8}).";
                return false;
            }

            string subject;
            try
            {
                using (var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath)))
                    subject = cert.Subject;
            }
            catch (Exception ex)
            {
                details = "Zertifikat des Herausgebers konnte nicht gelesen werden: " + ex.Message;
                return false;
            }

            foreach (var publisher in allowedPublishers)
            {
                if (SubjectMatches(subject, publisher))
                {
                    details = "Signiert von: " + subject;
                    return true;
                }
            }

            details = "Unerwarteter Herausgeber: " + subject;
            return false;
        }

        // Match against the O= or CN= part of the subject, not a loose substring of the whole string.
        private static bool SubjectMatches(string subject, string publisher)
        {
            foreach (var part in subject.Split(','))
            {
                var p = part.Trim();
                if (p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ||
                    p.StartsWith("O=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = p.Substring(p.IndexOf('=') + 1).Trim().Trim('"');
                    if (string.Equals(value, publisher, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        #region WinVerifyTrust interop

        private static readonly Guid WintrustActionGenericVerifyV2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        private static bool VerifyAuthenticode(string filePath, bool checkRevocation, out uint result)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)),
                pcwszFilePath = filePath
            };
            var pFile = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
            Marshal.StructureToPtr(fileInfo, pFile, false);

            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)),
                dwUIChoice = 2,              // WTD_UI_NONE
                fdwRevocationChecks = checkRevocation ? 1u : 0u, // WTD_REVOKE_WHOLECHAIN / WTD_REVOKE_NONE
                dwUnionChoice = 1,           // WTD_CHOICE_FILE
                pFile = pFile,
                dwStateAction = 1,           // WTD_STATEACTION_VERIFY
                dwProvFlags = checkRevocation ? 0x80u : 0u // WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT
            };

            var action = WintrustActionGenericVerifyV2;
            try
            {
                result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                return result == 0;
            }
            finally
            {
                data.dwStateAction = 2;      // WTD_STATEACTION_CLOSE
                WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                Marshal.DestroyStructure(pFile, typeof(WINTRUST_FILE_INFO));
                Marshal.FreeHGlobal(pFile);
            }
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        private static extern uint WinVerifyTrust(IntPtr hwnd, [In] ref Guid pgActionID, [In, Out] ref WINTRUST_DATA pWVTData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        #endregion
    }
}
