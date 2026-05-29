import {bindValue, trigger, useValue} from "cs2/api";
import {FocusBoundary} from "cs2/input";
import {useLocalization} from "cs2/l10n";
import {Button} from "cs2/ui";
import mod from "../../mod.json";
import {setVal} from "../api";
import {InputField} from "../util/input-field";
import styles from "./menu-styles.module.scss";
import React from "react";

const MIN_PORT = 1;
const MAX_PORT = 65535;

type TranslateFn = (id: string, fallback?: string | null) => string | null;

export const hostMenuVisible = bindValue<boolean>(mod.id, "HostMenuVisible", false);
export const modSupport = bindValue<Array<any>>(mod.id, "modSupport", []);
export const port = bindValue<number>(mod.id, "HostPort", 4230);
export const hostPassword = bindValue<string>(mod.id, "HostPassword", "");
export const username = bindValue<string>(mod.id, "Username", "");
export const playerStatus = bindValue<string>(mod.id, "PlayerStatus", "INACTIVE");
export const playerType = bindValue<string>(mod.id, "PlayerType", "NONE");

export function hideHostGame() {
    trigger(mod.id, "HideHostGameMenu");
}

export function setIntVal(name: string, value: any) {
    const parsed = Number.parseInt(value, 10);
    trigger(mod.id, name, Number.isNaN(parsed) ? 0 : parsed);
}

export function hostGame() {
    trigger(mod.id, "HostGame");
}

export function stopServer() {
    trigger(mod.id, "StopServer");
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

function getStatusText(translate: TranslateFn, status: string): string {
    return translateWithFallback(translate, `CS2M.UI.JoinStatus[${status}]`, status);
}

const ModCompatibilityList = ({supports}: { supports: any[] }) => {
    const {translate} = useLocalization();

    return (
        <div className={styles.compatibilityList}>
            {supports.map((s, i) => {
                let color = "var(--gWarning)";
                if (s.support === "Supported") color = "var(--gSuccess)";
                if (s.support === "Unsupported") color = "var(--gDanger)";

                return (
                    <div key={i} className={styles.item}>
                        <span>{s.name}</span>
                        <span style={{color}}>{translate(`CS2M.UI.Compatibility[${s.support}]`)}</span>
                    </div>
                );
            })}
        </div>
    );
};

export const HostGameMenu = () => {
    const visible = useValue(hostMenuVisible);
    const modSupports = useValue(modSupport);
    const status = useValue(playerStatus);
    const type = useValue(playerType);
    const isServerRunning = type === "SERVER";
    const isClientSession = type === "CLIENT" && status !== "INACTIVE";
    const enabled = !isServerRunning && !isClientSession;

    const portValue = useValue(port);
    const hostPasswordValue = useValue(hostPassword);
    const usernameValue = useValue(username);

    const {translate} = useLocalization();
    const statusText = getStatusText(translate, status);
    const portIsValid = isValidPort(portValue);
    const hasUsername = !!usernameValue?.trim();

    const validationErrors = React.useMemo(() => {
        const issues: string[] = [];
        if (isClientSession) {
            issues.push(translateWithFallback(translate, "CS2M.UI.Validation.HostBlockedByClient", "Disconnect from current server before hosting."));
        }
        if (!portIsValid) {
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
    }, [hasUsername, isClientSession, portIsValid, translate]);

    const canHost = enabled && validationErrors.length === 0;

    if (!visible) {
        return null;
    }

    return (
        <div className={styles.overlay}>
            <FocusBoundary>
                <div className={styles.card}>
                    <div className={styles.header}>
                        <h2>{translateWithFallback(translate, "CS2M.UI.HostGame", "Host Game")}</h2>
                        <Button className={styles.closeButton} onClick={hideHostGame}>X</Button>
                    </div>

                    <div className={styles.body}>
                        {enabled && validationErrors.length > 0 && (
                            <div className={styles.hintBox}>
                                <strong>{translateWithFallback(translate, "CS2M.UI.Validation.Title", "Missing or invalid input:")}</strong>
                                {validationErrors.map((message, index) => (
                                    <div key={`${message}-${index}`}>{message}</div>
                                ))}
                            </div>
                        )}

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.Host.Session", "Session")}
                            </div>
                            <div className={styles.summaryRow}>
                                <span>{translateWithFallback(translate, "CS2M.UI.Host.Mode", "Mode")}</span>
                                <span>{translateWithFallback(translate, "CS2M.UI.Host.Mode.Private", "Private Host")}</span>
                            </div>
                            <div className={styles.summaryRow}>
                                <span>{translateWithFallback(translate, "CS2M.UI.Host.JoinToken", "Join Token")}</span>
                                <span>{translateWithFallback(translate, "CS2M.UI.Host.JoinToken.Pending", "Generated after start")}</span>
                            </div>
                        </div>

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.NetworkConfig", "Network")}
                            </div>
                            <InputField
                                label={translateWithFallback(translate, "CS2M.UI.Port", "Port")}
                                value={portValue}
                                disabled={!enabled}
                                onChange={(val: string) => setIntVal("SetHostPort", val)}
                            />
                            <InputField
                                label={translateWithFallback(translate, "CS2M.UI.Password", "Password")}
                                value={hostPasswordValue}
                                type="password"
                                disabled={!enabled}
                                onChange={(val: string) => setVal("SetHostPassword", val)}
                            />
                        </div>

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.PlayerConfig", "Host Identity")}
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
                                {translateWithFallback(translate, "CS2M.UI.Host.Preflight", "Preflight")}
                            </div>
                            <div className={styles.preflightList}>
                                <div className={styles.preflightItem}>
                                    <span className={portIsValid ? styles.preflightOk : styles.preflightMissing}>
                                        {portIsValid ? "OK" : "MISSING"}
                                    </span>
                                    <span>{translateWithFallback(translate, "CS2M.UI.Host.Preflight.Port", "Port valid")}</span>
                                </div>
                                <div className={styles.preflightItem}>
                                    <span className={hasUsername ? styles.preflightOk : styles.preflightMissing}>
                                        {hasUsername ? "OK" : "MISSING"}
                                    </span>
                                    <span>{translateWithFallback(translate, "CS2M.UI.Host.Preflight.Username", "Username set")}</span>
                                </div>
                                <div className={styles.preflightItem}>
                                    <span className={isServerRunning ? styles.preflightNeutral : (isClientSession ? styles.preflightMissing : styles.preflightOk)}>
                                        {isServerRunning ? "RUNNING" : (isClientSession ? "BLOCKED" : "READY")}
                                    </span>
                                    <span>{translateWithFallback(translate, "CS2M.UI.Host.Preflight.State", "Host state")}</span>
                                </div>
                            </div>
                        </div>

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.Host.Runtime", "Runtime")}
                            </div>
                            <div className={styles.statusLine}>{statusText}</div>
                        </div>

                        {modSupports.length > 0 && enabled && (
                            <div className={styles.section}>
                                <div className={styles.sectionTitle}>
                                    {translateWithFallback(translate, "CS2M.UI.Compatibility", "Compatibility")}
                                </div>
                                <ModCompatibilityList supports={modSupports}/>
                            </div>
                        )}
                    </div>

                    <div className={styles.footer}>
                        <Button className={styles.button} onClick={hideHostGame}>
                            {translateWithFallback(translate, "CS2M.UI.Back", "Back")}
                        </Button>
                        {isClientSession && (
                            <Button className={styles.button} onClick={leaveSession}>
                                {translateWithFallback(translate, "CS2M.UI.LeaveSession", "Leave Session")}
                            </Button>
                        )}
                        {isServerRunning ? (
                            <Button className={`${styles.button} ${styles.primary}`} onClick={stopServer}>
                                {translateWithFallback(translate, "CS2M.UI.StopServer", "Stop Server")}
                            </Button>
                        ) : (
                            <Button className={`${styles.button} ${styles.primary}`} onClick={hostGame} disabled={!canHost}>
                                {translateWithFallback(translate, "CS2M.UI.StartServer", "Start Server")}
                            </Button>
                        )}
                    </div>
                </div>
            </FocusBoundary>
        </div>
    );
};

