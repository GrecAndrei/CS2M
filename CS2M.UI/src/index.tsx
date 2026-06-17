import "./styles/cs2m.scss";
import {getModule, ModRegistrar} from "cs2/modding";
import {MenuUIExtensions} from "./extends/main-menu";
import {ChatIcon, ChatPanel} from "./screens/chat";
import {CooperativeOverlay} from "./screens/cooperative-overlay";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.extend(
        "game-ui/common/animations/transition-group-coordinator.tsx",
        "TransitionGroupCoordinator",
        MenuUIExtensions,
    );

    moduleRegistry.append("GameBottomRight", ChatIcon);
    moduleRegistry.append("GameBottomRight", CooperativeOverlay);

    const gamePanelComponents = getModule(
        "game-ui/game/components/game-panel-renderer.tsx",
        "gamePanelComponents",
    );
    gamePanelComponents["CS2M.UI.ChatPanel"] = ChatPanel;
};

export default register;
