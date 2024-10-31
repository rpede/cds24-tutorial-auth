import { InitPasswordResetRequest } from "../../api";
import { SubmitHandler, useForm } from "react-hook-form";
import { useState } from "react";
import * as yup from "yup";
import { yupResolver } from "@hookform/resolvers/yup";
import toast from "react-hot-toast";
import { http } from "../../http";

const schema: yup.ObjectSchema<InitPasswordResetRequest> = yup
    .object({
        email: yup.string().email().required(),
    })
    .required();

export default function InitPasswordReset() {
    const {
        register,
        handleSubmit,
        formState: { errors },
    } = useForm({ resolver: yupResolver(schema) });
    const [success, setSuccess] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const onSubmit: SubmitHandler<InitPasswordResetRequest> = async (data) => {
        setError(null);
        try {
            const createPromise = http.authInitPasswordResetCreate(data);
            toast.promise(createPromise, {
                success: "Password reset email send",
                error: "Password reset failed",
                loading: "Hold on...",
            });
            await createPromise;
            setSuccess(true);
        } catch (e: any) {
            setError(e.message);
        }
    };
    
    if (success) return (
        <>
            <div role="alert" className="alert alert-success">
                <span>Password reset link was send to the entered email.</span>
            </div>
        </>
    );

    return (
        <>
            {error && (
                <div role="alert" className="alert alert-error">
                    <svg
                        onClick={() => setError(null)}
                        xmlns="http://www.w3.org/2000/svg"
                        className="stroke-current shrink-0 h-6 w-6"
                        fill="none"
                        viewBox="0 0 24 24"
                    >
                        <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeWidth="2"
                            d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"
                        />
                    </svg>
                    <span>{error}</span>
                </div>
            )}
            <div className="hero min-h-screen bg-base-200">
                <div className="hero-content flex-col lg:flex-row-reverse">
                    <div className="card shrink-0 w-full max-w-sm shadow-2xl bg-base-100">
                        <form
                            className="card-body"
                            method="post"
                            onSubmit={handleSubmit(onSubmit)}
                        >
                            <h2 className="card-title">Reset password</h2>
                            
                            <div className="form-control">
                                <label className="label">
                                    <span className="label-text">Email</span>
                                </label>
                                <input
                                    type="text"
                                    placeholder="email"
                                    className={`input input-bordered ${
                                        errors.email && "input-error"
                                    }`}
                                    {...register("email")}
                                />
                                <small className="text-error">{errors.email?.message}</small>
                            </div>
                            <div className="form-control mt-6">
                                <button className="btn btn-primary">Submit</button>
                            </div>
                        </form>
                    </div>
                </div>
            </div>
        </>
    );
}
