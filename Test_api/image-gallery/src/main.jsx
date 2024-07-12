import React from "react";
import ReactDOM from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import "./index.css";

import LoginPage from "./Pages/Login/LoginPage.jsx";
import RegisterPage from "./Pages/Register/RegisterPage.jsx";
import GoogleSignin from "./Pages/SocialSignin/GoogleSignin.jsx";
import FacebookSignin from "./Pages/SocialSignin/FacebookSignin.jsx";
import ResetPassword from "./Pages/ResetPassword/ResetPassword.jsx";
import HomePage from "./Pages/Home/HomePage.jsx";
import EmailConfirm from "./Pages/EmailValidationMessage/EmailConfirm.jsx";
import EmailSuccess from "./Pages/EmailValidationMessage/EmailSuccess.jsx";

const router = createBrowserRouter([
  {
    path: "/",
    element: <LoginPage />,
  },
  {
    path: "/register",
    element: <RegisterPage />,
  },
  {
    path: "/google-signin",
    element: <GoogleSignin />,
  },
  {
    path: "/facebook-signin",
    element: <FacebookSignin />,
  },
  {
    path: "/reset-password",
    element: <ResetPassword />,
  },
  {
    path: "/home",
    element: <HomePage />,
  },
  {
    path: "/email-confirm",
    element: <EmailConfirm />,
  },
  {
    path: "/email-success",
    element: <EmailSuccess />,
  },

  //add more routes
]);

export default function main() {
  return (
    <div>
      <RouterProvider router={router} />
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <RouterProvider router={router} />
  </React.StrictMode>
);
