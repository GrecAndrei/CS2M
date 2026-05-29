import {ModuleRegistryExtend} from "cs2/modding";
import {bindValue, trigger, useValue} from "cs2/api";
import mod from "../../mod.json";
import {MultiplayerHub} from "../screens/multiplayer-hub";
import {JoinGameMenu} from "../screens/join-game-menu";
import {HostGameMenu} from "../screens/host-game-menu";
import React from "react";

export function showMultiplayerMenu() {
    trigger(mod.id, "ShowMultiplayerMenu");
}

const hubMenuVisible = bindValue<boolean>(mod.id, "HubMenuVisible", false);
const joinMenuVisible = bindValue<boolean>(mod.id, "JoinMenuVisible", false);
const hostMenuVisible = bindValue<boolean>(mod.id, "HostMenuVisible", false);

// Extend TransitionGroupCoordinator as it is the only place we can put the JoinGameMenu
// Only extend it if it has 5 children => Main Menu or Pause Menu
export const MenuUIExtensions : ModuleRegistryExtend = (Component) => {
    return (props) => {
        const {children, ...otherProps} = props || {};
        const hubVisible = useValue(hubMenuVisible);
        const joinVisible = useValue(joinMenuVisible);
        const hostVisible = useValue(hostMenuVisible);
        const anyMenuVisible = hubVisible || joinVisible || hostVisible;
        const launcher = (
            <div
                style={{
                    position: "fixed",
                    right: "24px",
                    bottom: "24px",
                    zIndex: 9999
                }}>
                <button
                    type="button"
                    onClick={(event) => {
                        event.preventDefault();
                        event.stopPropagation();
                        showMultiplayerMenu();
                    }}
                    style={{
                        border: "none",
                        borderRadius: "999px",
                        padding: "10px 16px",
                        fontSize: "14px",
                        fontWeight: 700,
                        cursor: "pointer",
                        background: "#1f6feb",
                        color: "#ffffff"
                    }}>
                    Multiplayer
                </button>
            </div>
        );

        const menus =
            <>
                <MultiplayerHub></MultiplayerHub>
                <JoinGameMenu></JoinGameMenu>
                <HostGameMenu></HostGameMenu>
            </>;
        return (
            <Component {...otherProps}>
                {children}
                {!anyMenuVisible && launcher}
                {menus}
            </Component>
        )
    };
}
