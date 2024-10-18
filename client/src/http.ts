import { Api } from "./api";
import { tokenStorage, TOKEN_KEY } from "./atoms/auth.ts";

// URL prefix for own server
// This is to protect us from accidently sending the JWT to 3rd party services.
const AUTHORIZE_ORIGIN = "/";

const _api = new Api();

_api.instance.interceptors.request.use((config) => {
    // Get the JWT from storage.
    const jwt = tokenStorage.getItem(TOKEN_KEY, null);
    // Add Authorization header if we have a JWT and the request goes to our own
    // server.
    if (jwt && config.url?.startsWith(AUTHORIZE_ORIGIN)) {
        // Set Authorization header, so server can tell hos is logged in.
        config.headers.Authorization = `Bearer ${jwt}`;
    }
    return config;
});

// Expose API-client which will handle authorization.
export const http = _api.api;
 