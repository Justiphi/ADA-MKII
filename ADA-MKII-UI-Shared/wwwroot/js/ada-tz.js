// The viewer's IANA time zone, e.g. "Pacific/Auckland".
//
// Used because the web head runs on a server whose own zone says nothing about
// where the user is. The MAUI WebView answers with the device zone, so the same
// call is correct on every head.
export function resolvedTimeZone() {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone ?? '';
    } catch {
        return '';
    }
}
