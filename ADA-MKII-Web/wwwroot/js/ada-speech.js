// Web Speech API bridge for ADA-MKII-Web.
//
// Recognition is Chrome/Edge only in practice, and on Chrome the audio is sent
// to Google's servers - see CLAUDE.md. Synthesis is far more widely supported.
// Both require a secure context, so this does nothing over plain HTTP except on
// localhost.

let recognition = null;

const Recognition = window.SpeechRecognition ?? window.webkitSpeechRecognition;

export function recognitionSupported() {
    return Recognition !== undefined && Recognition !== null;
}

export function synthesisSupported() {
    return typeof window.speechSynthesis !== 'undefined';
}

/**
 * Starts listening. Results are pushed back to .NET rather than returned,
 * because they arrive over time - the caller turns them into an async stream.
 */
export function startRecognition(dotNetRef, culture) {
    if (!recognitionSupported()) {
        dotNetRef.invokeMethodAsync('OnError', 'Speech recognition is not supported in this browser.');
        return false;
    }

    stopRecognition();

    recognition = new Recognition();
    recognition.lang = culture || 'en-US';
    recognition.continuous = false;
    recognition.interimResults = true;

    recognition.onresult = (event) => {
        // Only the last result matters; earlier ones are already reflected in it.
        const result = event.results[event.results.length - 1];
        dotNetRef.invokeMethodAsync('OnResult', result[0].transcript, result.isFinal);
    };

    recognition.onerror = (event) => {
        // "no-speech" and "aborted" are ordinary outcomes, not failures worth
        // showing the user.
        if (event.error === 'no-speech' || event.error === 'aborted') {
            dotNetRef.invokeMethodAsync('OnEnded');
            return;
        }
        dotNetRef.invokeMethodAsync('OnError', describeError(event.error));
    };

    recognition.onend = () => dotNetRef.invokeMethodAsync('OnEnded');

    try {
        recognition.start();
        return true;
    } catch (err) {
        dotNetRef.invokeMethodAsync('OnError', err?.message ?? 'Could not start listening.');
        return false;
    }
}

export function stopRecognition() {
    if (recognition) {
        try {
            recognition.onend = null;
            recognition.abort();
        } catch {
            // Already stopped; nothing to do.
        }
        recognition = null;
    }
}

export function speak(text) {
    if (!synthesisSupported() || !text) {
        return false;
    }

    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    window.speechSynthesis.speak(utterance);
    return true;
}

export function stopSpeaking() {
    if (synthesisSupported()) {
        window.speechSynthesis.cancel();
    }
}

function describeError(code) {
    switch (code) {
        case 'not-allowed':
        case 'service-not-allowed':
            return 'Microphone access was denied. Allow it in the browser and try again.';
        case 'audio-capture':
            return 'No microphone was found.';
        case 'network':
            return 'Speech recognition needs a network connection.';
        default:
            return `Speech recognition failed (${code}).`;
    }
}
