import { setupWorker } from 'msw/browser';
import { staffHandlers } from './handlers';

export const worker = setupWorker(...staffHandlers);
