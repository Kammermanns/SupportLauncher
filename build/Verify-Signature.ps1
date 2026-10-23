param(
    [Parameter(Mandatory)] [string] $Path,
    [string] $ExpectedPublisher = 'SignPath Foundation'
)

# Fails the build if the file is not validly signed by the expected publisher.
# A signing request can report success without having signed what matters,
# so the result is checked explicitly.
$sig = Get-AuthenticodeSignature -FilePath $Path
if ($sig.Status -ne 'Valid') {
    Write-Error "$Path : signature status is '$($sig.Status)'"
    exit 1
}
if ($sig.SignerCertificate.Subject -notmatch [regex]::Escape($ExpectedPublisher)) {
    Write-Error "$Path : unexpected signer '$($sig.SignerCertificate.Subject)'"
    exit 1
}
Write-Host "$Path : OK ($($sig.SignerCertificate.Subject))"
