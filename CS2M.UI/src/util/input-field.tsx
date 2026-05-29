import React from 'react';
import styles from "./input-field.module.scss";

interface InputFieldProps {
    label?: string | null;
    value: string | number;
    onChange: (val: string) => void;
    placeholder?: string;
    type?: string;
    disabled?: boolean;
    className?: string; // Allow overriding/extending
}

export const InputField = ({
    label,
    value,
    onChange,
    placeholder,
    type = "text",
    disabled = false,
    className
}: InputFieldProps) => {

    return (
        <div className={styles.formGroup}>
            {label && <label>{label}</label>}
            <input
                type={type}
                className={`${styles.formControl} ${className || ''} ${disabled ? styles.inputDisabled : ''}`}
                value={value}
                onChange={(e) => onChange(e.target.value)}
                placeholder={placeholder}
                disabled={disabled}
            />
        </div>
    );
}
