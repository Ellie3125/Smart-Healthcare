import { setupServer } from 'msw/node';
import { patientHandlers } from './handlers';

export const server = setupServer(...patientHandlers);
