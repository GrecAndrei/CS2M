import {bindValue, trigger, useValue} from "cs2/api";
import {FocusBoundary} from "cs2/input";
import {useLocalization} from "cs2/l10n";
import {Button} from "cs2/ui";
import mod from "../../mod.json";
import styles from "./menu-styles.module.scss";
import React from "react";

type TranslateFn = (id: string, fallback?: string | null) => string | null;

const hubMenuVisible = bindValue<boolean>(mod.id, "HubMenuVisible", false);
const playerStatus = bindValue<string>(mod.id, "PlayerStatus", "INACTIVE");
const playerType = bindValue<string>(mod.id, "PlayerType", "NONE");

function translateWithFallback(translate: TranslateFn, key: string, fallback: string): string {
    const translated = translate(key);
    return !translated || translated === key ? fallback : translated;
}

function getStatusText(translate: TranslateFn, status: string): string {
    return translateWithFallback(translate, `CS2M.UI.JoinStatus[${status}]`, status);
}

function showJoinMenu() {
    trigger(mod.id, "ShowJoinGameMenu");
}

function showHostMenu() {
    trigger(mod.id, "ShowHostGameMenu");
}

function hideHub() {
    trigger(mod.id, "HideMultiplayerHub");
}

function leaveSession() {
    trigger(mod.id, "LeaveSession");
}

export const MultiplayerHub = () => {
    const visible = useValue(hubMenuVisible);
    const status = useValue(playerStatus);
    const type = useValue(playerType);
    const {translate} = useLocalization();

    const isServerRunning = type === "SERVER";
    const isClientSession = type === "CLIENT" && status !== "INACTIVE";
    const sessionActive = type !== "NONE" || status !== "INACTIVE";
    const canJoin = !isServerRunning && !isClientSession;
    const canHost = !isServerRunning && !isClientSession;

    if (!visible) {
        return null;
    }

    return (
        <div className={styles.overlay}>
            <FocusBoundary>
                <div className={styles.card}>
                    <div className={styles.header}>
                        <h2>{translateWithFallback(translate, "CS2M.UI.MultiplayerHub", "Multiplayer Hub")}</h2>
                        <Button className={styles.closeButton} onClick={hideHub}>X</Button>
                    </div>

                    <div className={styles.body}>
                        <div className={styles.hubGrid}>
                            <Button className={styles.hubActionCard} onClick={showJoinMenu} disabled={!canJoin}>
                                <div className={styles.hubActionTitle}>
                                    {translateWithFallback(translate, "CS2M.UI.JoinGame", "Join Game")}
                                </div>
                                <div className={styles.hubActionText}>
                                    {translateWithFallback(
                                        translate,
                                        "CS2M.UI.Hub.Join.Description",
                                        "Connect to an existing server using token or IP."
                                    )}
                                </div>
                            </Button>

                            <Button className={styles.hubActionCard} onClick={showHostMenu} disabled={!canHost}>
                                <div className={styles.hubActionTitle}>
                                    {translateWithFallback(translate, "CS2M.UI.HostGame", "Host Game")}
                                </div>
                                <div className={styles.hubActionText}>
                                    {translateWithFallback(
                                        translate,
                                        "CS2M.UI.Hub.Host.Description",
                                        "Start a server and let other players join your city."
                                    )}
                                </div>
                            </Button>
                        </div>

                        <div className={styles.section}>
                            <div className={styles.sectionTitle}>
                                {translateWithFallback(translate, "CS2M.UI.Hub.CurrentStatus", "Current Status")}
                            </div>
                            <div className={styles.statusLine}>{getStatusText(translate, status)}</div>
                            {isServerRunning && (
                                <div className={styles.inputNote}>
                                    {translateWithFallback(
                                        translate,
                                        "CS2M.UI.Hub.ServerRunningHint",
                                        "Server session active. Stop hosting before switching to Join."
                                    )}
                                </div>
                            )}
                            {isClientSession && (
                                <div className={styles.inputNote}>
                                    {translateWithFallback(
                                        translate,
                                        "CS2M.UI.Hub.ClientRunningHint",
                                        "Client session active. Leave the current server before hosting."
                                    )}
                                </div>
                            )}
                        </div>
                    </div>

                    <div className={styles.footer}>
                        {sessionActive && (
                            <Button className={styles.button} onClick={leaveSession}>
                                {translateWithFallback(translate, "CS2M.UI.LeaveSession", "Leave Session")}
                            </Button>
                        )}
                        <Button className={styles.button} onClick={hideHub}>
                            {translateWithFallback(translate, "Common.CANCEL", "Cancel")}
                        </Button>
                    </div>
                </div>
            </FocusBoundary>
        </div>
    );
};
