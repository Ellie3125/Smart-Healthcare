import { setupServer } from 'msw/node';
import { staffHandlers } from './handlers';

export const server = setupServer(...staffHandlers);
