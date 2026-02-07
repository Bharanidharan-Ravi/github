import { useEffect } from "react";
import { useSignalR } from "./useSignalR";
import { joinGroup, leaveGroup } from "../group";

export function useGroup(groupName, opts = { autoJoin: true }) {
    useSignalR();

    useEffect(() => {
        let mounted = true;
        async function join() {
            try {
                await joinGroup(groupName);
            } catch (err) {
                if (!mounted) return;
                console.warn("useGroup join failed", err);
            }
        }
        if (opts.autoJoin) join();

        return () => {
            mounted = false;
            leaveGroup(groupName).catch(() => {});
        };
    }, [groupName]);
}