import {bindValue, trigger} from "cs2/api";
import mod from "../mod.json";

export const PlayerStatus = {
    INACTIVE: "INACTIVE",
    LOADING_MAP: "DOWNLOADING_MAP",
    CONNECTING: "CONNECTING",
    CONNECTED: "CONNECTED",
    ERROR: "ERROR",
} as const;

export const PlayerType = {
    NONE: "NONE",
    CLIENT: "CLIENT",
    SERVER: "SERVER",
} as const;

export const state = {
    hubMenuVisible: bindValue<boolean>(mod.id, "HubMenuVisible", false),
    joinMenuVisible: bindValue<boolean>(mod.id, "JoinMenuVisible", false),
    hostMenuVisible: bindValue<boolean>(mod.id, "HostMenuVisible", false),

    modSupport: bindValue<Array<{name: string; support: string}>>(mod.id, "modSupport", []),

    joinIpAddress: bindValue<string>(mod.id, "JoinIpAddress", ""),
    joinToken: bindValue<string>(mod.id, "JoinToken", ""),
    joinPort: bindValue<number>(mod.id, "JoinPort", 4230),
    joinPassword: bindValue<string>(mod.id, "JoinPassword", ""),

    hostPort: bindValue<number>(mod.id, "HostPort", 4230),
    hostPassword: bindValue<string>(mod.id, "HostPassword", ""),

    username: bindValue<string>(mod.id, "Username", ""),
    playerStatus: bindValue<string>(mod.id, "PlayerStatus", PlayerStatus.INACTIVE),
    playerType: bindValue<string>(mod.id, "PlayerType", PlayerType.NONE),

    downloadDone: bindValue<number>(mod.id, "DownloadDone", 0),
    downloadRemaining: bindValue<number>(mod.id, "DownloadRemaining", 0),
    downloadSpeed: bindValue<number>(mod.id, "DownloadSpeed", 0),

    joinErrorMessage: bindValue<Array<string>>(mod.id, "JoinErrorMessage", []),

    cooperativeData: bindValue<string>(mod.id, "CooperativeData", "{}"),
} as const;

export const actions = {
    showMultiplayerMenu: () => trigger(mod.id, "ShowMultiplayerMenu"),
    showJoinMenu: () => trigger(mod.id, "ShowJoinGameMenu"),
    showHostMenu: () => trigger(mod.id, "ShowHostGameMenu"),
    hideHub: () => trigger(mod.id, "HideMultiplayerHub"),
    hideJoin: () => trigger(mod.id, "HideJoinGameMenu"),
    hideHost: () => trigger(mod.id, "HideHostGameMenu"),

    setJoinIpAddress: (value: string) => trigger(mod.id, "SetJoinIpAddress", value),
    setJoinToken: (value: string) => trigger(mod.id, "SetJoinToken", value),
    setJoinPort: (value: string) => {
        const parsed = Number.parseInt(value, 10);
        trigger(mod.id, "SetJoinPort", Number.isNaN(parsed) ? 4230 : parsed);
    },
    setJoinPassword: (value: string) => trigger(mod.id, "SetJoinPassword", value),

    setHostPort: (value: string) => {
        const parsed = Number.parseInt(value, 10);
        trigger(mod.id, "SetHostPort", Number.isNaN(parsed) ? 4230 : parsed);
    },
    setHostPassword: (value: string) => trigger(mod.id, "SetHostPassword", value),

    setUsername: (value: string) => trigger(mod.id, "SetUsername", value),

    joinGame: () => trigger(mod.id, "JoinGame"),
    hostGame: () => trigger(mod.id, "HostGame"),
    stopServer: () => trigger(mod.id, "StopServer"),
    leaveSession: () => trigger(mod.id, "LeaveSession"),
} as const;

export const isServerRunning = (type: string) => type === PlayerType.SERVER;
export const isClientSession = (type: string, status: string) =>
    type === PlayerType.CLIENT && status !== PlayerStatus.INACTIVE;
export const sessionActive = (type: string, status: string) =>
    isServerRunning(type) || isClientSession(type, status);
