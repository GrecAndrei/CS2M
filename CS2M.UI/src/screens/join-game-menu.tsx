import {bindValue, trigger, useValue} from "cs2/api";
import {FocusBoundary} from "cs2/input";
import {LocalizedNumber, Unit, useLocalization} from "cs2/l10n";
import {Button} from "cs2/ui";
import mod from "../../mod.json";
import {setVal} from "../api";
import {InputField} from "../util/input-field";
import styles from "./menu-styles.module.scss";
import React from "react";

const MIN_PORT = 1;
const MAX_PORT = 65535;
const RECENT_SERVERS_KEY = "CS2M.RecentServers";
const MAX_RECENT_SERVERS = 6;

type TranslateFn = (id: string, fallback?: string | null) => string | null;
type ConnectionMode = "token" | "direct";

type RecentServer = {
    mode: ConnectionMode;
    ipAddress?: string;
    port?: number;
    token?: string;
    savedAt: number;
};

export const joinMenuVisible = bindValue<boolean>(mod.id, "JoinMenuVisible", false);
export const modSupport = bindValue<Array<any>>(mod.id, "modSupport", []);
export const ipAddress = bindValue<string>(mod.id, "JoinIpAddress", "");
export const joinToken = bindValue<string>(mod.id, "JoinToken", "");
export const port = bindValue<number>(mod.id, "JoinPort", 4230);
export const joinPassword = bindValue<string>(mod.id, "JoinPassword", "");
export const username = bindValue<string>(mod.id, "Username", "");
export const playerStatus = bindValue<string>(mod.id, "PlayerStatus", "INACTIVE");
export const playerType = bindValue<string>(mod.id, "PlayerType", "NONE");
export const downloadDone = bindValue<number>(mod.id, "DownloadDone", 0);
export const downloadRemaining = bindValue<number>(mod.id, "DownloadRemaining", 0);
export const downloadSpeed = bindValue<number>(mod.id, "DownloadSpeed", 0);
export const joinErrorMessage = bindValue<Array<string>>(mod.id, "JoinErrorMessage", []);

export function hideJoinGame() {
    trigger(mod.id, "HideJoinGameMenu");
}

export function setIntVal(name: string, value: any) {
    const parsed = Number.parseInt(value, 10);
    trigger(mod.id, name, Number.isNaN(parsed) ? 0 : parsed);
}

export function joinGame() {
    trigger(mod.id, "JoinGame");
}

export function leaveSession() {
    trigger(mod.id, "LeaveSession");
}

function isValidPort(value: number): boolean {
    return Number.isInteger(value) && value >= MIN_PORT && value <= MAX_PORT;
}

function translateWithFallback(translate: TranslateFn, key: string, fallback: string): string {
    const translated = translate(key);
    return !translated || translated === key ? fallback : translated;
}

function applyTemplate(template: string, replacements: Record<string, string>): string {
    return Object.entries(replacements).reduce(
        (result, [name, value]) => result.replaceAll(`{${name}}`, value),
        template
    );
}

function getStatusText(translate: TranslateFn, status: string): string {
    return translateWithFallback(translate, `CS2M.UI.JoinStatus[${status}]`, status);
}

function resolveJoinErrors(rawErrors: string[], translate: TranslateFn): string[] {
    const messages: string[] = [];

    for (let i = 0; i < rawErrors.length; i++) {
        const value = rawErrors[i];
        if (!value) {
            continue;
        }

        if (!value.startsWith("precondition:")) {
            messages.push(translateWithFallback(translate, value, value));
            continue;
        }

        const code = value.substring(13);
        const baseKey = `CS2M.UI.JoinError.${code}`;
        switch (code) {
            case "GAME_VERSION_MISMATCH":
            case "MOD_VERSION_MISMATCH": {
                const serverVersion = rawErrors[i + 1] ?? "?";
                const clientVersion = rawErrors[i + 2] ?? "?";
                const template = translateWithFallback(translate, baseKey, code);
                messages.push(applyTemplate(template, {SERVER: serverVersion, CLIENT: clientVersion}));
                i += 2;
                break;
            }
            case "DLCS_MISMATCH":
            case "MODS_MISMATCH": {
                const serverMissing = rawErrors[i + 1] || "-";
                const clientMissing = rawErrors[i + 2] || "-";
                messages.push(translateWithFallback(translate, baseKey, code));
                messages.push(
                    applyTemplate(
                        translateWithFallback(translate, `${baseKey}.server`, `Server mismatch: ${serverMissing}`),
                        {SERVER: serverMissing}
                    )
                );
                messages.push(
                    applyTemplate(
                        translateWithFallback(translate, `${baseKey}.client`, `Client mismatch: ${clientMissing}`),
                        {CLIENT: clientMissing}
                    )
                );
                i += 2;
                break;
            }
            default:
                messages.push(translateWithFallback(translate, baseKey, code));
                break;
        }
    }

    return messages;
}

function getRecentServerKey(server: RecentServer): string {
    return server.mode === "token"
        ? `token:${server.token ?? ""}`
        : `direct:${server.ipAddress ?? ""}:${server.port ?? 0}`;
}

function readRecentServers(): RecentServer[] {
    try {
        if (typeof localStorage === "undefined") {
            return [];
        }
        const raw = localStorage.getItem(RECENT_SERVERS_KEY);
        if (!raw) {
            return [];
        }
        const parsed = JSON.parse(raw);
        if (!Array.isArray(parsed)) {
            return [];
        }
        return parsed
            .filter((entry) => entry && (entry.mode === "direct" || entry.mode === "token"))
            .slice(0, MAX_RECENT_SERVERS);
    } catch {
        return [];
    }
}

function writeRecentServers(servers: RecentServer[]) {
    try {
        if (typeof localStorage !== "undefined") {
            localStorage.setItem(RECENT_SERVERS_KEY, JSON.stringify(servers));
        }
    } catch {
        // Keep join flow functional even if storage fails.
    }
}

function formatRecentServerLabel(server: RecentServer): string {
    if (server.mode === "token") {
        const token = server.token ?? "";
        if (token.length <= 18) {
            return token;
        }
        return `${token.slice(0, 7)}...${token.slice(-7)}`;
    }
    return `${server.ipAddress ?? "?"}:${server.port ?? 0}`;
}

const MessageBox = ({title, lines, className}: { title: string; lines: string[]; className: string }) => {
    if (lines.length === 0) {
        return null;
    }

    return (
        <div className={className}>
            <strong>{title}</strong>
            {lines.map((line, i) => (
                <div key={`${line}-${i}`}>{line}</div>
            ))}
        </div>
    );
};

export const JoinGameMenu = () => {
    const visible = useValue(joinMenuVisible);
    const status = useValue(playerStatus);
    const type = useValue(playerType);
    const dlDone = useValue(downloadDone);
    const dlRemaining = useValue(downloadRemaining);
    const dlSpeed = useValue(downloadSpeed);
    const errors = useValue(joinErrorMessage);

    const ipAddressValue = useValue(ipAddress);
    const joinTokenValue = useValue(joinToken);
    const portValue = useValue(port);
    const joinPasswordValue = useValue(joinPassword);
    const usernameValue = useValue(username);

    const isClientSession = type === "CLIENT" && status !== "INACTIVE";
    const isServerSession = type === "SERVER";
    const sessionActive = isClientSession || isServerSession;
    const enabled = !isClientSession && !isServerSession;
    const {translate} = useLocalization();
    const [recentServers, setRecentServers] = React.useState<RecentServer[]>([]);
    const [connectionMode, setConnectionMode] = React.useState<ConnectionMode>("direct");

    React.useEffect(() => {
        setRecentServers(readRecentServers());
    }, []);

    React.useEffect(() => {
        if (joinTokenValue?.trim()) {
            setConnectionMode("token");
        }
    }, [joinTokenValue]);

    const usingToken = connectionMode === "token";
    const hasTokenValue = !!joinTokenValue?.trim();
    const hasDirectTarget = !!ipAddressValue?.trim();
    const hasUsername = !!usernameValue?.trim();
    const portIsValid = isValidPort(portValue);

    const validationErrors = React.useMemo(() => {
        const issues: string[] = [];
        if (isServerSession) {
            issues.push(translateWithFallback(translate, "CS2M.UI.Validation.JoinBlockedByServer", "Stop hosting before joining."));
        }
        if (isClientSession) {
            issues.push(translateWithFallback(translate, "CS2M.UI.Validation.JoinInProgress", "Join is already in progress."));
        }
        if (usingToken && !hasTokenValue) {
            issues.push(translateWithFallback(translate, "CS2M.UI.Validation.TokenRequired", "Server token is required."));
        }
        if (!usingToken && !hasDirectTarget) {
            issues.push(translateWithFallback(translate, "CS2M.UI.Validation.ServerRequired", "Server IP is required."));
        }
        if (!usingToken && !portIsValid) {
            issues.push(
                translateWithFallback(
                    translate,
                    "CS2M.UI.Validation.PortInvalid",
                    `Port must be between ${MIN_PORT} and ${MAX_PORT}.`
                )
            );
        }
        if (!hasUsername) {
            issues.push(
                translateWithFallback(
                    translate,
                    "CS2M.UI.Validation.UsernameRequired",
                    "Username is required."
                )
            );
        }
        return issues;
    }, [hasDirectTarget, hasTokenValue, hasUsername, isClientSession, isServerSession, portIsValid, translate, usingToken]);

    const parsedErrors = React.useMemo(() => resolveJoinErrors(errors, translate), [errors, translate]);
    const canJoin = enabled && validationErrors.length === 0;
    const statusText = getStatusText(translate, status);

    const saveCurrentServer = React.useCallback(() => {
        const token = joinTokenValue?.trim();
        let server: RecentServer | null = null;
        if (usingToken && token) {
            server = {
                mode: "token",
                token,
                savedAt: Date.now()
            };
        } else if (!usingToken && ipAddressValue?.trim() && isValidPort(portValue)) {
            server = {
                mode: "direct",
                ipAddress: ipAddressValue.trim(),
                port: portValue,
                savedAt: Date.now()
            };
        }

        if (!server) {
            return;
        }

        setRecentServers((previous) => {
            const currentKey = getRecentServerKey(server as RecentServer);
            const deduplicated = previous.filter((entry) => getRecentServerKey(entry) !== currentKey);
            const next = [server as RecentServer, ...deduplicated].slice(0, MAX_RECENT_SERVERS);
            writeRecentServers(next);
            return next;
        });
    }, [ipAddressValue, joinTokenValue, portValue, usingToken]);

    const applyRecentServer = React.useCallback((server: RecentServer) => {
        if (server.mode === "token") {
            setConnectionMode("token");
            setVal("SetJoinToken", server.token ?? "");
            return;
        }

        setConnectionMode("direct");
        setVal("SetJoinToken", "");
        setVal("SetJoinIpAddress", server.ipAddress ?? "");
        setIntVal("SetJoinPort", server.port ?? 4230);
    }, []);

    const clearRecentServers = React.useCallback(() => {
        setRecentServers([]);
        writeRecentServers([]);
    }, []);

    const handleJoinGame = React.useCallback(() => {
        if (!canJoin) {
            return;
        }
        saveCurrentServer();
        joinGame();
    }, [canJoin, saveCurrentServer]);

    if (!visible) {
        return null;
    }

    let statusDisplay = null;
    if (status === "DOWNLOADING_MAP") {
        const dlTotal = (dlDone + dlRemaining) || 1;
        const doneMib = dlDone / 1024 / 1024;
        const totalMib = dlTotal / 1024 / 1024;
        const speedMib = dlSpeed / 1024 / 1024;
        const progressPercent = `${(dlDone / dlTotal) * 100}%`;

        statusDisplay = (
            <div className={`${styles.section} ${styles.downloadStatus}`}>
                <div className={styles.sectionTitle}>{statusText}</div>
                <div className={styles.progressMeta}>
                    <LocalizedNumber value={doneMib} unit={Unit.FloatSingleFraction} /> /{" "}
                    <LocalizedNumber value={totalMib} unit={Unit.FloatSingleFraction} /> MiB
                    (<LocalizedNumber value={speedMib} unit={Unit.FloatSingleFraction} /> MiB/s)
                </div>
                <div className={styles.progressTrack}>
                    <div className={styles.progressFill} style={{width: progressPercent}}/>
                </div>
            </div>
        );
    } else if (status !== "INACTIVE") {
        statusDisplay = (
            <div className={`${styles.section} ${styles.statusInline}`}>
                {statusText}
            </div>
        );
    }

    return (
        <div className={styles.overlay}>
            <FocusBoundary>
                <div className={styles.card}>
                    <div className={styles.header}>
                        <h2>{translateWithFallback(translate, "CS2M.UI.JoinGame", "Join Game")}</h2>
                        <Button className={styles.closeButton} onClick={hideJoinGame}>X</Button>
                    </div>

                    <div className={styles.body}>
                        <MessageBox
                            title={translateWithFallback(translate, "CS2M.UI.JoinError.Intro", "Join failed:")}
                            lines={parsedErrors}
                            className={styles.errorBox}
                        />
                        {enabled && (
                            <MessageBox
                                title={translateWithFallback(translate, "CS2M.UI.Validation.Title", "Missing or invalid input:")}
                                lines={validationErrors}
                                className={styles.hintBox}
                            />
                        )}

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.Join.ConnectionMethod", "Connection Method")}
                            </div>
                            <div className={styles.modeToggle}>
                                <Button
                                    className={`${styles.modeOption} ${usingToken ? styles.modeOptionActive : ""}`}
                                    onClick={() => setConnectionMode("token")}
                                    disabled={!enabled}
                                >
                                    {translateWithFallback(translate, "CS2M.UI.Token", "Token")}
                                </Button>
                                <Button
                                    className={`${styles.modeOption} ${!usingToken ? styles.modeOptionActive : ""}`}
                                    onClick={() => setConnectionMode("direct")}
                                    disabled={!enabled}
                                >
                                    {translateWithFallback(translate, "CS2M.UI.Join.Direct", "IP + Port")}
                                </Button>
                            </div>
                        </div>

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.ConnectionDetails", "Connection Details")}
                            </div>
                            {usingToken ? (
                                <InputField
                                    label={translateWithFallback(translate, "CS2M.UI.Token", "Server Token")}
                                    value={joinTokenValue}
                                    disabled={!enabled}
                                    onChange={(val: string) => setVal("SetJoinToken", val)}
                                />
                            ) : (
                                <>
                                    <InputField
                                        label={translateWithFallback(translate, "CS2M.UI.IPAddress", "IP Address")}
                                        value={ipAddressValue}
                                        disabled={!enabled}
                                        onChange={(val: string) => setVal("SetJoinIpAddress", val)}
                                    />
                                    <InputField
                                        label={translateWithFallback(translate, "CS2M.UI.Port", "Port")}
                                        value={portValue}
                                        disabled={!enabled}
                                        onChange={(val: string) => setIntVal("SetJoinPort", val)}
                                    />
                                </>
                            )}
                            <InputField
                                label={translateWithFallback(translate, "CS2M.UI.Password", "Password")}
                                value={joinPasswordValue}
                                type="password"
                                disabled={!enabled}
                                onChange={(val: string) => setVal("SetJoinPassword", val)}
                            />
                        </div>

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.PlayerConfig", "Player")}
                            </div>
                            <InputField
                                label={translateWithFallback(translate, "CS2M.UI.Username", "Username")}
                                value={usernameValue}
                                disabled={!enabled}
                                onChange={(val: string) => setVal("SetUsername", val)}
                            />
                        </div>

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.Join.Preflight", "Preflight")}
                            </div>
                            <div className={styles.preflightList}>
                                <div className={styles.preflightItem}>
                                    <span className={hasUsername ? styles.preflightOk : styles.preflightMissing}>
                                        {hasUsername ? "OK" : "MISSING"}
                                    </span>
                                    <span>{translateWithFallback(translate, "CS2M.UI.Join.Preflight.Username", "Username set")}</span>
                                </div>
                                <div className={styles.preflightItem}>
                                    <span className={usingToken ? (hasTokenValue ? styles.preflightOk : styles.preflightMissing) : (hasDirectTarget ? styles.preflightOk : styles.preflightMissing)}>
                                        {(usingToken ? hasTokenValue : hasDirectTarget) ? "OK" : "MISSING"}
                                    </span>
                                    <span>
                                        {usingToken
                                            ? translateWithFallback(translate, "CS2M.UI.Join.Preflight.Token", "Token provided")
                                            : translateWithFallback(translate, "CS2M.UI.Join.Preflight.Server", "Server address set")}
                                    </span>
                                </div>
                                <div className={styles.preflightItem}>
                                    <span className={usingToken ? styles.preflightNeutral : (portIsValid ? styles.preflightOk : styles.preflightMissing)}>
                                        {usingToken ? "SKIP" : (portIsValid ? "OK" : "MISSING")}
                                    </span>
                                    <span>{translateWithFallback(translate, "CS2M.UI.Join.Preflight.Port", "Port valid")}</span>
                                </div>
                            </div>
                        </div>

                        {recentServers.length > 0 && (
                            <div className={styles.section}>
                                <div className={styles.sectionHeaderRow}>
                                    <div className={styles.sectionTitle}>
                                        {translateWithFallback(translate, "CS2M.UI.RecentServers", "Recent Servers")}
                                    </div>
                                    <Button className={styles.linkAction} disabled={!enabled} onClick={clearRecentServers}>
                                        {translateWithFallback(translate, "CS2M.UI.ClearRecent", "Clear")}
                                    </Button>
                                </div>
                                <div className={styles.recentList}>
                                    {recentServers.map((server, index) => (
                                        <div className={styles.recentItem} key={`${getRecentServerKey(server)}:${index}`}>
                                            <span>{formatRecentServerLabel(server)}</span>
                                            <Button
                                                className={styles.compactButton}
                                                onClick={() => applyRecentServer(server)}
                                                disabled={!enabled}
                                            >
                                                {translateWithFallback(translate, "CS2M.UI.Use", "Use")}
                                            </Button>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        )}

                        {statusDisplay}
                    </div>

                    <div className={styles.footer}>
                        <Button className={styles.button} onClick={hideJoinGame}>
                            {translateWithFallback(translate, "CS2M.UI.Back", "Back")}
                        </Button>
                        {sessionActive && (
                            <Button className={styles.button} onClick={leaveSession}>
                                {translateWithFallback(translate, "CS2M.UI.LeaveSession", "Leave Session")}
                            </Button>
                        )}
                        <Button className={`${styles.button} ${styles.primary}`} onClick={handleJoinGame} disabled={!canJoin}>
                            {translateWithFallback(translate, "CS2M.UI.JoinGame", "Join Game")}
                        </Button>
                    </div>
                </div>
            </FocusBoundary>
        </div>
    );
};

