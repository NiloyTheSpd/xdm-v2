"use strict";
export default class Logger {
    constructor() {
        // Off by default: per-request logging (full header maps, whole sync
        // payload every few seconds) is a measurable CPU/IO cost on media-heavy
        // pages such as YouTube. Flip to true only while debugging.
        this.loggingEnabled = false;
    }

    log(content) {
        if (this.loggingEnabled) {
            console.log(content);
        }
    }
}