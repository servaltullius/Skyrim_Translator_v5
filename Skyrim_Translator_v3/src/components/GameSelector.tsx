// Modern React doesn't need explicit React import for JSX

interface GameOption {
  value: string;
  label: string;
  fullName: string;
}

interface GameSelectorProps {
  selectedGame: string;
  onGameChange: (gameValue: string) => void;
  disabled?: boolean;
}

const gameOptions: GameOption[] = [
  { value: 'skyrim', label: '🏰 스카이림', fullName: 'The Elder Scrolls V: Skyrim' },
  { value: 'fallout4', label: '☢️ 폴아웃 4', fullName: 'Fallout 4' },
  { value: 'fallout_new_vegas', label: '🎰 뉴베가스', fullName: 'Fallout: New Vegas' },
  { value: 'oblivion', label: '🏛️ 오블리비언', fullName: 'The Elder Scrolls IV: Oblivion' },
  { value: 'starfield', label: '🚀 스타필드', fullName: 'Starfield' },
];

export function GameSelector({ selectedGame, onGameChange, disabled = false }: GameSelectorProps) {
  return (
    <select
      value={selectedGame}
      onChange={(e) => onGameChange(e.target.value)}
      disabled={disabled}
      className="toolbar-select transition-colors"
      style={{ minWidth: '130px' }}
      title={gameOptions.find(g => g.value === selectedGame)?.fullName}
    >
      {gameOptions.map(game => (
        <option key={game.value} value={game.value}>
          {game.label}
        </option>
      ))}
    </select>
  );
}

export { gameOptions };
export type { GameOption };