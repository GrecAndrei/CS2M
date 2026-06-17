import {useLocalization} from "cs2/l10n";
import {PlayerStatus} from "../state";

export type TranslateFn = (key: string, fallback: string) => string;

export function useTranslate(): TranslateFn {
    const {translate} = useLocalization();
    return (key: string, fallback: string) => {
        const result = translate(key, fallback);
        if (!result || result === key) {
            return fallback;
        }
        return result;
    };
}

export function compatibilityText(t: TranslateFn, level: string): string {
    return t(`CS2M.UI.Compatibility[${level}]`, level);
}

export const statusToLabel: Record<string, string> = {
    [PlayerStatus.INACTIVE]: "Idle",
    [PlayerStatus.LOADING_MAP]: "Downloading map",
    [PlayerStatus.CONNECTING]: "Connecting",
    [PlayerStatus.CONNECTED]: "Connected",
    [PlayerStatus.ERROR]: "Error",
};
