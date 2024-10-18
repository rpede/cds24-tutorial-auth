import { Api } from "./api";

const _api = new Api();
export const http = _api.api;
