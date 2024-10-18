import { atom, useAtom } from "jotai";
import { AuthUserInfo } from "../api";
import { http } from "../http";
import { useNavigate } from "react-router-dom";

export type Credentials = { email: string; password: string };

type AuthHook = {
  user: AuthUserInfo | null;
  login: (credentials: Credentials) => Promise<void>;
  logout: () => void;
};

const isLoggedInAtom = atom(false);

const userInfoAtom = atom(async (get) => {
  if (get(isLoggedInAtom)) {
    const response = await http.authUserinfoList();
    return response.data;
  } else {
    return null;
  }
});

export const useAuth = () => {
  const [_, setIsLoggedIn] = useAtom(isLoggedInAtom);
  const [user] = useAtom(userInfoAtom);
  const navigate = useNavigate();

  const login = async (credentials: Credentials) => {
    await http.authLoginCreate(credentials);
    setIsLoggedIn(true);
    navigate("/");
  };

  const logout = async () => {
    await http.authLogoutCreate();
    navigate("/login");
  };

  return {
    user,
    login,
    logout,
  } as AuthHook;
};