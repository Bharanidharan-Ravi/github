import { useEffect } from "react";
import { useCustomStore } from "shared-store";
import { getActiveGroups, joinGroup, leaveGroup } from "../group";


const moduleGroups = {
    tickets: "tickets",
    atteendance: "attendance",
};

export function useActiveModuleSync() {
    const { activeModule } = useCustomStore();

    useEffect(() => {
        const group = moduleGroups[activeModule];
        console.log("group :", group);

        // Object.values(moduleGroups).forEach(g => leaveGroup(g));
        getActiveGroups().forEach(g => leaveGroup(g));

        if (group) joinGroup(group);
    }, [activeModule]);
}