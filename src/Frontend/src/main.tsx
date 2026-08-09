import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

// Poppins is bundled, not fetched from a font CDN: the page has to render
// identically with no outbound network access. Only the latin subset ships —
// the full package also carries devanagari, which is 200 kB this UI never uses.
import '@fontsource/poppins/latin-300.css';
import '@fontsource/poppins/latin-400.css';
import '@fontsource/poppins/latin-500.css';
import '@fontsource/poppins/latin-600.css';

import './styles/globals.css';
import { App } from './app/App';

const container = document.getElementById('root');
if (!container) throw new Error('Elemento #root não encontrado.');

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
