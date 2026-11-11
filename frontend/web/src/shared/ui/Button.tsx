import React from 'react';
import { Loader2 } from 'lucide-react';

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger' | 'warning';
export type ButtonSize = 'sm' | 'md' | 'lg';

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  leftIcon?: React.ReactNode;
}

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: 'bg-[#006a41] text-white hover:bg-[#005231] focus-visible:ring-[#006a41]',
  secondary: 'bg-[#e5f8ed] text-[#005231] border border-[#9ef5c0] hover:bg-[#d4e7dc] focus-visible:ring-[#006a41]',
  outline: 'bg-white text-[#0f1f18] border border-[#bdcabf] hover:bg-[#e9f2ed] focus-visible:ring-[#006a41]',
  ghost: 'bg-transparent text-[#3e4941] hover:bg-[#e9f2ed] focus-visible:ring-[#006a41]',
  danger: 'bg-[#ba1a1a] text-white hover:bg-[#93000a] focus-visible:ring-[#ba1a1a]',
  warning: 'bg-[#fef3c7] text-[#9a5b00] border border-[#f59e0b]/40 hover:bg-[#fde68a] focus-visible:ring-[#9a5b00]',
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: 'min-h-[36px] px-3 py-1.5 text-xs gap-1.5 rounded-lg',
  md: 'min-h-[40px] px-4 py-2 text-sm gap-2 rounded-lg',
  lg: 'min-h-[44px] px-5 py-2.5 text-base gap-2 rounded-xl',
};

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = 'primary', size = 'md', loading = false, leftIcon, disabled, children, className = '', type = 'button', ...rest }, ref) => {
    const isDisabled = Boolean(disabled || loading);
    return (
      <button
        ref={ref}
        type={type}
        disabled={isDisabled}
        aria-busy={loading || undefined}
        className={`inline-flex items-center justify-center font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1 disabled:opacity-50 disabled:cursor-not-allowed ${VARIANT_CLASSES[variant]} ${SIZE_CLASSES[size]} ${className}`}
        {...rest}
      >
        {loading ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : leftIcon ? <span aria-hidden="true">{leftIcon}</span> : null}
        <span>{children}</span>
      </button>
    );
  },
);

Button.displayName = 'Button';
