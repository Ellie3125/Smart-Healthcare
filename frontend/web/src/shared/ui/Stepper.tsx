import React from 'react';
import { Check } from 'lucide-react';

export interface StepItem {
  id: string;
  label: string;
  description?: string;
}

export const Stepper: React.FC<{ steps: StepItem[]; currentStepIndex: number }> = ({
  steps,
  currentStepIndex,
}) => (
  <ol className="flex flex-wrap items-center gap-3" aria-label="Tiến trình thực hiện">
    {steps.map((step, idx) => {
      const done = idx < currentStepIndex;
      const active = idx === currentStepIndex;
      return (
        <li key={step.id} className="flex items-center gap-2">
          <span
            className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold ${
              done
                ? 'bg-[#006a41] text-white'
                : active
                  ? 'border-2 border-[#006a41] bg-[#e5f8ed] text-[#005231]'
                  : 'border border-[#cddbd3] bg-white text-[#44544c]'
            }`}
          >
            {done ? <Check className="h-3.5 w-3.5" aria-hidden="true" /> : idx + 1}
          </span>
          <div>
            <p className={`text-xs font-semibold ${active || done ? 'text-[#0f1f18]' : 'text-[#44544c]'}`}>
              {step.label}
            </p>
            {step.description ? <p className="text-[11px] text-[#6e7a71]">{step.description}</p> : null}
          </div>
          {idx < steps.length - 1 ? <span className="mx-1 h-px w-6 bg-[#cddbd3]" aria-hidden="true" /> : null}
        </li>
      );
    })}
  </ol>
);

export interface TabItem {
  id: string;
  label: string;
  count?: number;
}

export const Tabs: React.FC<{
  tabs: TabItem[];
  activeId: string;
  onChange: (id: string) => void;
}> = ({ tabs, activeId, onChange }) => (
  <div role="tablist" className="flex flex-wrap gap-1 border-b border-[#cddbd3]">
    {tabs.map((tab) => {
      const active = tab.id === activeId;
      return (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={active}
          onClick={() => onChange(tab.id)}
          className={`inline-flex items-center gap-1.5 border-b-2 px-4 py-2.5 text-sm font-medium transition-colors ${
            active
              ? 'border-[#006a41] text-[#006a41]'
              : 'border-transparent text-[#44544c] hover:text-[#0f1f18]'
          }`}
        >
          <span>{tab.label}</span>
          {tab.count !== undefined ? (
            <span className="rounded-full bg-[#e9f2ed] px-2 py-0.5 text-xs tabular-nums text-[#0f1f18]">
              {tab.count}
            </span>
          ) : null}
        </button>
      );
    })}
  </div>
);
