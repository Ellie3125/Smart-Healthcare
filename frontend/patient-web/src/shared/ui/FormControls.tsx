import React, { useId } from 'react';

export interface FormFieldProps {
  label: string;
  htmlFor?: string;
  required?: boolean;
  hint?: string;
  error?: string;
  children: (controlProps: { id: string; 'aria-describedby'?: string; 'aria-invalid'?: true }) => React.ReactNode;
  className?: string;
}

export const FormField: React.FC<FormFieldProps> = ({
  label,
  htmlFor,
  required,
  hint,
  error,
  children,
  className = '',
}) => {
  const generatedId = useId();
  const id = htmlFor ?? `patient-field-${generatedId}`;
  const hintId = hint ? `${id}-hint` : undefined;
  const errId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errId].filter(Boolean).join(' ') || undefined;

  return (
    <div className={`space-y-1.5 ${className}`}>
      <label htmlFor={id} className="block text-sm font-medium text-[#0f1f18]">
        {label}
        {required ? (
          <span className="ml-1 text-[#ba1a1a]" aria-hidden="true">
            *
          </span>
        ) : null}
      </label>
      {children({
        id,
        'aria-describedby': describedBy,
        ...(error ? { 'aria-invalid': true as const } : {}),
      })}
      {hint && !error ? (
        <p id={hintId} className="text-xs text-[#3e4941]">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errId} role="alert" className="text-xs font-medium text-[#ba1a1a]">
          {error}
        </p>
      ) : null}
    </div>
  );
};

const CONTROL_CLASSES =
  'w-full rounded-xl border border-[#bdcabf] bg-white px-3.5 py-2.5 text-base sm:text-sm text-[#0f1f18] placeholder:text-[#6e7a71] focus:border-[#006a41] focus:outline-none focus:ring-2 focus:ring-[#006a41]/20 disabled:bg-[#e5f8ed] disabled:text-[#3e4941]';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  hasError?: boolean;
}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(({ hasError, className = '', ...rest }, ref) => (
  <input
    ref={ref}
    className={`${CONTROL_CLASSES} min-h-[44px] ${hasError ? 'border-[#ba1a1a] focus:border-[#ba1a1a] focus:ring-[#ba1a1a]/20' : ''} ${className}`}
    {...rest}
  />
));
Input.displayName = 'Input';

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  options?: SelectOption[];
  placeholder?: string;
  hasError?: boolean;
}

export const Select = React.forwardRef<HTMLSelectElement, SelectProps>(
  ({ options, placeholder, hasError, children, className = '', ...rest }, ref) => (
    <select
      ref={ref}
      className={`${CONTROL_CLASSES} min-h-[44px] ${hasError ? 'border-[#ba1a1a]' : ''} ${className}`}
      {...rest}
    >
      {placeholder ? <option value="">{placeholder}</option> : null}
      {options
        ? options.map((opt) => (
            <option key={opt.value} value={opt.value} disabled={opt.disabled}>
              {opt.label}
            </option>
          ))
        : children}
    </select>
  ),
);
Select.displayName = 'Select';

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  hasError?: boolean;
}

export const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(({ hasError, className = '', ...rest }, ref) => (
  <textarea
    ref={ref}
    className={`${CONTROL_CLASSES} min-h-[96px] ${hasError ? 'border-[#ba1a1a]' : ''} ${className}`}
    {...rest}
  />
));
Textarea.displayName = 'Textarea';

export const DatePicker = React.forwardRef<HTMLInputElement, Omit<InputProps, 'type'>>((props, ref) => (
  <Input ref={ref} type="date" {...props} />
));
DatePicker.displayName = 'DatePicker';
