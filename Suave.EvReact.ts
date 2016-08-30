module Suave.EvReact {
    interface Callback { (data: any): void; }

    export function remoteCallback(url: string, callback: Callback, rawText?: boolean): Callback {
        return data => {
            const request = new XMLHttpRequest();
            request.open('POST', url, true);
            request.setRequestHeader('Content-Type', 'application/json');
            request.onload = () => {
                if (request.status < 200 || request.status >= 400)
                    console.error(url, request.status, request.statusText);
                else if (callback) {
                    const arg = rawText ? request.responseText : JSON.parse(request.responseText);
                    callback(arg);
                }
            };
            request.onerror = () => console.error(url, request.status, request.statusText);
            request.send(JSON.stringify(data));
        };
    }

    export class EventRequest {
        private listeners: Callback[] = [];
        private callback: Callback;

        constructor(url: string) {
            const dispatch = data => {
                this.listeners.forEach(handler => {
                    handler(data);
                });
            };
            this.callback = remoteCallback(url, dispatch);
        }

        trigger(data) {
            this.callback(data);
        }

        addListener(cb: Callback) {
            this.listeners.push(cb);
        }

        removeListener(cb: Callback) {
            const idx = this.listeners.lastIndexOf(cb);
            if (idx != -1)
                this.listeners.splice(idx, 1);
        }
    }
}
