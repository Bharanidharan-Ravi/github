import { useEffect } from "react";
import { useCustomStore } from "shared-store";
import { createConnection, start } from "../connection";
import { reattachAllHandlers } from "../events";

export function useSignalR(autoStart = true) {
  const { token } = useCustomStore();

  useEffect(() => {
    createConnection();
    if (autoStart && token) {
      start()
        .then(() => {
          // Ensure handlers are attached after connection starts
          reattachAllHandlers();
        })
        .catch((err) => console.error("SignalR Init Failed", err));
    }
    // if (autoStart && token) {
    //   init();
    //   //   start()
    //   //     .then(() => reattachAllHandlers())
    //   //     .catch(() => {});
    // }
    // const init = async () => {
    //   const conn = await start();
    //   if (conn) {
    //     conn.onreconnected(() => reattachAllHandlers());
    //   }
    // };
  }, [token, autoStart]);
}
