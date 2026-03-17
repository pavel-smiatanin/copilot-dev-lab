import { BrowserRouter, Route, Routes } from 'react-router-dom';
import HomePage from './pages/HomePage';
import StatsPage from './pages/StatsPage';
import UnlockPage from './pages/UnlockPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/stats/:id" element={<StatsPage />} />
        <Route path="/unlock/:alias" element={<UnlockPage />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
