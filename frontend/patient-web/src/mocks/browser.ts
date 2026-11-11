import { setupWorker } from 'msw/browser';
import { patientHandlers } from './handlers';

export const worker = setupWorker(...patientHandlers);
