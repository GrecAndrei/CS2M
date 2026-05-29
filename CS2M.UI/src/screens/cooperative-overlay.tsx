import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import mod from "../../mod.json";
import styles from "./cooperative.module.scss";

interface RemoteCursor {
    playerId: number;
    username: string;
    x: number;
    y: number;
    z: number;
    screenX: number;
    screenY: number;
    visible: boolean;
    tool: string;
    prefab: string;
}

interface RemotePing {
    playerId: number;
    username: string;
    x: number;
    y: number;
    z: number;
    screenX: number;
    screenY: number;
    visible: boolean;
    distance: number;
    type: number;
    remaining: number;
}

interface RosterPlayer {
    playerId: number;
    username: string;
    type: string;
    latency: number;
    tool: string;
    prefab: string;
}

interface CooperativeData {
    cursors: RemoteCursor[];
    pings: RemotePing[];
    players: RosterPlayer[];
}

const cooperativeDataBinding = bindValue<string>(mod.id, 'CooperativeData', "{}");

// Generate stable aesthetic colors for player ids
function getPlayerColor(playerId: number): string {
    if (playerId === -1) return "#00f2fe"; // Host neon blue
    const hue = (playerId * 137.5) % 360;
    return `hsl(${hue}, 95%, 60%)`;
}

// Helper to format tool/prefab names cleanly
function formatActivity(tool: string, prefab: string): string {
    if (!tool || tool === "None" || tool === "DefaultToolSystem") {
        return "Inspecting View";
    }
    const cleanTool = tool.replace("ToolSystem", "");
    if (prefab) {
        return `Building: ${prefab}`;
    }
    return `Using: ${cleanTool}`;
}

// Synthesise a satisfying futuristic chime using Web Audio API
function playPingSound() {
    try {
        const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
        if (!AudioCtx) return;
        
        const ctx = new AudioCtx();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        
        osc.type = "sine";
        osc.connect(gain);
        gain.connect(ctx.destination);
        
        const now = ctx.currentTime;
        
        // Fast dual sine tone sweeps from 880Hz (A5) to 1320Hz (E6)
        osc.frequency.setValueAtTime(880, now);
        osc.frequency.exponentialRampToValueAtTime(1320, now + 0.12);
        
        gain.gain.setValueAtTime(0.18, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.28);
        
        osc.start(now);
        osc.stop(now + 0.3);
    } catch (e) {
        // Safe fail-silent if AudioContext is blocked or unsupported
    }
}

export const CooperativeOverlay = () => {
    const rawData = useValue(cooperativeDataBinding);
    
    let data: CooperativeData = { cursors: [], pings: [], players: [] };
    try {
        if (rawData) {
            data = JSON.parse(rawData);
        }
    } catch (e) {
        // Safe fallback on initialization
    }

    const [lastPingCount, setLastPingCount] = useState(0);

    // Audio chime trigger hook
    useEffect(() => {
        const pingCount = data.pings?.length ?? 0;
        if (pingCount > lastPingCount) {
            playPingSound();
        }
        setLastPingCount(pingCount);
    }, [data.pings, lastPingCount]);

    const handleTeleport = (playerId: number) => {
        trigger(mod.id, "TeleportToPlayer", playerId);
    };

    const localPlayerId = data.players.find(p => p.type === "SERVER" || p.playerId === 0)?.playerId ?? 0;

    return (
        <div className={styles.overlayCanvas}>
            
            {/* 1. REAL-TIME PLAYER ROSTER HUD */}
            {data.players && data.players.length > 0 && (
                <div className={styles.rosterHud}>
                    <div className={styles.hudHeader}>
                        <span className={styles.hudTitle}>Co-Op Lobby</span>
                        <span className={styles.playerCount}>
                            {data.players.length} {data.players.length === 1 ? "Builder" : "Builders"}
                        </span>
                    </div>

                    <div className={styles.playerList}>
                        {data.players.map((player) => {
                            const isLocal = player.playerId === localPlayerId;
                            const playerColor = getPlayerColor(player.playerId);
                            return (
                                <div key={player.playerId} className={styles.playerRow}>
                                    <div className={styles.playerInfo}>
                                        <span 
                                            className={styles.statusDot} 
                                            style={{ background: playerColor, boxShadow: `0 0 8px ${playerColor}` }} 
                                        />
                                        <div className={styles.nameContainer}>
                                            <span className={styles.playerName}>
                                                {player.username} {isLocal && "(You)"}
                                            </span>
                                            <span className={styles.playerStatusText}>
                                                {formatActivity(player.tool, player.prefab)}
                                            </span>
                                        </div>
                                    </div>

                                    <div className={styles.actions}>
                                        <span className={styles.latency}>
                                            {player.latency > 0 ? `${player.latency}ms` : "Host"}
                                        </span>
                                        
                                        {!isLocal && (
                                            <button 
                                                className={styles.teleportBtn}
                                                onClick={() => handleTeleport(player.playerId)}
                                                title={`Teleport camera to ${player.username}`}
                                            >
                                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                                                    <circle cx="12" cy="12" r="10" />
                                                    <circle cx="12" cy="12" r="3" />
                                                    <path d="M12 2v4M12 18v4M2 12h4M18 12h4" />
                                                </svg>
                                            </button>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}

            {/* 2. REAL-TIME 3D PROJECTED GHOST CURSORS */}
            {data.cursors && data.cursors.map((cursor) => {
                if (!cursor.visible) return null;
                const playerColor = getPlayerColor(cursor.playerId);
                return (
                    <div 
                        key={`cursor-${cursor.playerId}`}
                        className={styles.ghostCursor}
                        style={{ 
                            left: `${cursor.screenX}%`, 
                            top: `${cursor.screenY}%`,
                            ["--player-color" as any]: playerColor 
                        }}
                    >
                        <div className={styles.pointerRing} />
                        <div className={styles.cursorTag}>
                            <span className={styles.name}>{cursor.username}</span>
                            {(cursor.prefab || cursor.tool) && (
                                <span className={styles.tool}>
                                    {cursor.prefab ? cursor.prefab : cursor.tool.replace("ToolSystem", "")}
                                </span>
                            )}
                        </div>
                    </div>
                );
            })}

            {/* 3. INTERACTIVE WORLD PING BEACONS */}
            {data.pings && data.pings.map((ping, idx) => {
                if (!ping.visible) return null;
                const playerColor = getPlayerColor(ping.playerId);
                return (
                    <div
                        key={`ping-${ping.playerId}-${idx}`}
                        className={styles.pingBeacon}
                        style={{ 
                            left: `${ping.screenX}%`, 
                            top: `${ping.screenY}%`
                        }}
                    >
                        <div className={styles.pulseCircle}>
                            <div className={styles.ring} style={{ borderColor: playerColor, boxShadow: `0 0 15px ${playerColor}` }} />
                            <div className={styles.coreDot} style={{ borderColor: playerColor, boxShadow: `0 0 10px ${playerColor}` }} />
                        </div>
                        <div className={styles.pingLabel} style={{ borderColor: playerColor }}>
                            <span className={styles.pingUser}>{ping.username}</span>
                            <span className={styles.pingDist}>{Math.round(ping.distance)}m</span>
                        </div>
                    </div>
                );
            })}

        </div>
    );
};
