using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;

namespace cli_life
{
    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> neighbors = new List<Cell>();
        private bool IsAliveNext;

        public void DetermineNextLiveState()
        {
            int liveNeighbors = neighbors.Count(x => x.IsAlive);
            if (IsAlive)
                IsAliveNext = liveNeighbors == 2 || liveNeighbors == 3;
            else
                IsAliveNext = liveNeighbors == 3;
        }

        public void Advance()
        {
            IsAlive = IsAliveNext;
        }
    }

    public class Board
    {
        public readonly Cell[,] Cells;
        public readonly int CellSize;

        public int Columns => Cells.GetLength(0);
        public int Rows => Cells.GetLength(1);
        public int Width => Columns * CellSize;
        public int Height => Rows * CellSize;

        public Board(int width, int height, int cellSize, double liveDensity = 0.1)
        {
            CellSize = cellSize;
            Cells = new Cell[width / cellSize, height / cellSize];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    Cells[x, y] = new Cell();

            ConnectNeighbors();
            Randomize(liveDensity);
        }

        private readonly Random rand = new Random();

        public void Randomize(double liveDensity)
        {
            foreach (var cell in Cells)
                cell.IsAlive = rand.NextDouble() < liveDensity;
        }

        public void Advance()
        {
            foreach (var cell in Cells)
                cell.DetermineNextLiveState();
            foreach (var cell in Cells)
                cell.Advance();
        }

        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int xL = (x > 0) ? x - 1 : Columns - 1;
                    int xR = (x < Columns - 1) ? x + 1 : 0;
                    int yT = (y > 0) ? y - 1 : Rows - 1;
                    int yB = (y < Rows - 1) ? y + 1 : 0;

                    Cells[x, y].neighbors.Add(Cells[xL, yT]);
                    Cells[x, y].neighbors.Add(Cells[x, yT]);
                    Cells[x, y].neighbors.Add(Cells[xR, yT]);
                    Cells[x, y].neighbors.Add(Cells[xL, y]);
                    Cells[x, y].neighbors.Add(Cells[xR, y]);
                    Cells[x, y].neighbors.Add(Cells[xL, yB]);
                    Cells[x, y].neighbors.Add(Cells[x, yB]);
                    Cells[x, y].neighbors.Add(Cells[xR, yB]);
                }
            }
        }
    }

    public class Config
    {
        public int width { get; set; }
        public int height { get; set; }
        public int cellSize { get; set; }
        public double liveDensity { get; set; }
    }

    public class Pattern
    {
        public string Name { get; set; }
        public bool[,] Shape { get; set; }
        public string Type { get; set; }
    }

    public class Program
    {
        static Board board;
        static int generation = 0;
        static List<int> history = new List<int>();
        const int stableLimit = 5;
        static List<Pattern> patterns = new List<Pattern>();
        static Dictionary<string, int> patternCounts = new Dictionary<string, int>();
        static List<double> densityResults = new List<double>();
        static List<int> stableGenerations = new List<int>();

        static Config LoadConfig(string path)
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(json);
        }

        static void SaveState(string path)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < board.Rows; y++)
            {
                for (int x = 0; x < board.Columns; x++)
                {
                    sb.Append(board.Cells[x, y].IsAlive ? '1' : '0');
                }
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString());
        }

        static void LoadState(string path)
        {
            var lines = File.ReadAllLines(path);
            for (int y = 0; y < board.Rows && y < lines.Length; y++)
                for (int x = 0; x < board.Columns && x < lines[y].Length; x++)
                    board.Cells[x, y].IsAlive = lines[y][x] == '1';
        }

        static int CountAlive()
        {
            return board.Cells.Cast<Cell>().Count(c => c.IsAlive);
        }

        static bool IsStable()
        {
            if (history.Count < stableLimit)
                return false;

            int last = history.Last();
            return history.Skip(history.Count - stableLimit).All(v => v == last);
        }

        static void InitializePatterns()
        {
            patterns.Add(new Pattern
            {
                Name = "Block",
                Type = "Stable",
                Shape = new bool[,] { { true, true }, { true, true } }
            });
            patterns.Add(new Pattern
            {
                Name = "Beehive",
                Type = "Stable",
                Shape = new bool[,] { { false, true, true, false }, { true, false, false, true }, { false, true, true, false } }
            });
            patterns.Add(new Pattern
            {
                Name = "Blinker",
                Type = "Periodic",
                Shape = new bool[,] { { true, true, true } }
            });
            patterns.Add(new Pattern
            {
                Name = "Glider",
                Type = "Moving",
                Shape = new bool[,] { { false, true, false }, { false, false, true }, { true, true, true } }
            });
            patterns.Add(new Pattern
            {
                Name = "Toad",
                Type = "Periodic",
                Shape = new bool[,] { { false, true, true, true }, { true, true, true, false } }
            });
            patterns.Add(new Pattern
            {
                Name = "Eater",
                Type = "Eater",
                Shape = new bool[,] { { true, true, false, false }, { true, false, true, false }, { false, false, true, true } }
            });
        }

        static void AnalyzePatterns()
        {
            patternCounts.Clear();
            foreach (var pattern in patterns)
                patternCounts[pattern.Name] = 0;

            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    foreach (var pattern in patterns)
                    {
                        if (CheckPatternAt(x, y, pattern))
                            patternCounts[pattern.Name]++;
                    }
                }
            }
        }

        static bool CheckPatternAt(int x, int y, Pattern pattern)
        {
            int height = pattern.Shape.GetLength(0);
            int width = pattern.Shape.GetLength(1);

            if (x + width > board.Columns || y + height > board.Rows)
                return false;

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    if (board.Cells[x + j, y + i].IsAlive != pattern.Shape[i, j])
                        return false;

            return true;
        }

        static int CountClusters()
        {
            bool[,] visited = new bool[board.Columns, board.Rows];
            int clusters = 0;

            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    if (board.Cells[x, y].IsAlive && !visited[x, y])
                    {
                        FloodFill(x, y, visited);
                        clusters++;
                    }
                }
            }
            return clusters;
        }

        static void FloodFill(int x, int y, bool[,] visited)
        {
            if (x < 0 || x >= board.Columns || y < 0 || y >= board.Rows || visited[x, y] || !board.Cells[x, y].IsAlive)
                return;

            visited[x, y] = true;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        FloodFill(x + dx, y + dy, visited);
        }

        static void GeneratePlot()
        {
            int width = 800, height = 600;
            using (Bitmap bmp = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                Font font = new Font("Arial", 10);
                Pen pen = new Pen(Color.Black, 2);
                int margin = 50;
                int maxGen = generation;
                int maxAlive = history.Any() ? history.Max() : 1;

                g.DrawLine(pen, margin, height - margin, width - margin, height - margin);
                g.DrawLine(pen, margin, height - margin, margin, margin);
                g.DrawString("Generation", font, Brushes.Black, width / 2, height - margin + 10);
                g.DrawString("Alive Cells", font, Brushes.Black, 10, 10);

                for (int i = 0; i <= 5; i++)
                {
                    int x = margin + i * (width - 2 * margin) / 5;
                    int y = height - margin - i * (height - 2 * margin) / 5;
                    g.DrawString((maxGen * i / 5).ToString(), font, Brushes.Black, x, height - margin + 10);
                    g.DrawString((maxAlive * i / 5).ToString(), font, Brushes.Black, 10, y - 10);
                }

                for (int i = 1; i < history.Count; i++)
                {
                    int x1 = margin + (i - 1) * (width - 2 * margin) / maxGen;
                    int y1 = height - margin - history[i - 1] * (height - 2 * margin) / maxAlive;
                    int x2 = margin + i * (width - 2 * margin) / maxGen;
                    int y2 = height - margin - history[i] * (height - 2 * margin) / maxAlive;
                    g.DrawLine(pen, x1, y1, x2, y2);
                }

                bmp.Save("plot.png", ImageFormat.Png);
            }
        }

        static void Reset()
        {
            var config = LoadConfig("settings.json");
            board = new Board(config.width, config.height, config.cellSize, config.liveDensity);
            generation = 0;
            history.Clear();
            File.WriteAllText("data.txt", "Generation\tAlive\n");
            InitializePatterns();
        }

        static void Render()
        {
            Console.SetCursorPosition(0, 0);
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    var cell = board.Cells[col, row];
                    Console.Write(cell.IsAlive ? '*' : ' ');
                }
                Console.Write('\n');
            }
            Console.WriteLine($"Generation: {generation}");
            Console.WriteLine($"Alive: {CountAlive()}");
            Console.WriteLine($"Clusters: {CountClusters()}");
            AnalyzePatterns();
            foreach (var kvp in patternCounts)
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            Console.WriteLine("S - Save, L - Load, R - Reset, Q - Quit");
        }

        static void RunSimulation(double density)
        {
            var config = LoadConfig("settings.json");
            config.liveDensity = density;
            board = new Board(config.width, config.height, config.cellSize, config.liveDensity);
            generation = 0;
            history.Clear();
            File.WriteAllText($"data_{density}.txt", "Generation\tAlive\n");

            while (generation < 1000 && !IsStable())
            {
                Render();
                int alive = CountAlive();
                history.Add(alive);
                File.AppendAllText($"data_{density}.txt", $"{generation}\t{alive}\n");
                board.Advance();
                generation++;
                Thread.Sleep(100);
            }
            stableGenerations.Add(generation);
            Console.WriteLine($"Плотность {density}: Стабилизация на поколении {generation}");
        }

        static void Main(string[] args)
        {
            Console.CursorVisible = false;
            Reset();

            // Исследование стабильности для разных плотностей
            double[] densities = { 0.1, 0.3, 0.5, 0.7 };
            foreach (var d in densities)
            {
                Console.Clear();
                Console.WriteLine($"Симуляция для плотности {d}");
                RunSimulation(d);
                densityResults.Add(d);
                GeneratePlot(); // Сохраняем график для каждой плотности
                Console.WriteLine("Нажмите любую клавишу для продолжения...");
                Console.ReadKey();
            }

            // Сохраняем последний график как основной
            File.Copy($"data_{densities.Last()}.txt", "data.txt", true);
            GeneratePlot();

            // Загрузка и тестирование фигур
            string[] patternFiles = { "glider.txt", "blinker.txt", "beehive.txt" };
            foreach (var file in patternFiles)
            {
                if (File.Exists(file))
                {
                    Console.Clear();
                    Console.WriteLine($"Тестирование фигуры из {file}");
                    Reset();
                    LoadState(file);
                    int maxGen = 100;
                    for (int i = 0; i < maxGen && !IsStable(); i++)
                    {
                        Render();
                        int alive = CountAlive();
                        history.Add(alive);
                        File.AppendAllText("data.txt", $"{generation}\t{alive}\n");
                        board.Advance();
                        generation++;
                        Thread.Sleep(200);
                    }
                    Console.WriteLine("Нажмите любую клавишу для продолжения...");
                    Console.ReadKey();
                }
            }

            // Основной интерактивный цикл
            Console.Clear();
            Reset();
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.S)
                        SaveState("save.txt");
                    else if (key == ConsoleKey.L)
                        LoadState("save.txt");
                    else if (key == ConsoleKey.R)
                        Reset();
                    else if (key == ConsoleKey.Q)
                        break;
                }

                Render();
                int alive = CountAlive();
                File.AppendAllText("data.txt", $"{generation}\t{alive}\n");

                history.Add(alive);
                if (IsStable())
                {
                    Console.WriteLine(">>> Система стабилизировалась.");
                    GeneratePlot();
                    break;
                }

                board.Advance();
                generation++;
                Thread.Sleep(200);
            }

            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}