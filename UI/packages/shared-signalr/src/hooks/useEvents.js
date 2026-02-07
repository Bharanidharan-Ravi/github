import { useEffect, useRef } from "react";
import { addEventHandler, removeEventHandler } from "../events";

export function useEvent(eventName, callback) {
    // 1. Use a ref to keep the callback fresh without restarting the effect
    const savedCallback = useRef(callback);

    useEffect(() => {
        savedCallback.current = callback;
    });

    useEffect(() => {
        // 2. The handler wrapper calls the current version of the callback
        const handler = (...args) => {
            if (savedCallback.current) {
                savedCallback.current(...args);
            }
        };

        // 3. Register with our custom registry
        if (eventName && eventName !== "___IgnoreCreated") {
            addEventHandler(eventName, handler);
        }

        return () => {
            if (eventName) removeEventHandler(eventName, handler);
        };
    }, [eventName]);
}

// export function useEvent(eventName, callback) {
//     const cbRef = useRef(callback);
//     cbRef.current = callback;

//     useEffect(() => {
//         const wrapper = (...args) => cbRef.current(...args);
//         addEventListener(eventName, wrapper);
//         return () => removeEventListener(eventName, wrapper);
//     }, [eventName]);
// }