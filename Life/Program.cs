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
        const int stableLimit = 10; // Увеличено для предотвращения ранней стабилизации
        static List<Pattern> patterns = new List<Pattern>();
        static Dictionary<string, int> patternCounts = new Dictionary<string, int>();
        static List<double> densityResults = new List<double>();
        static List<int> stableGenerations = new List<int>();

        static Config LoadConfig(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Config>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации из {path}: {ex.Message}");
                return new Config { width = 50, height = 20, cellSize = 1, liveDensity = 0.1 };
            }
        }

        static void SaveState(string filename)
        {
            string path = Path.Combine(GetProjectDirectory(), filename);
            try
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
                Console.WriteLine($"Состояние сохранено в {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения состояния в {path}: {ex.Message}");
            }
        }

        static void LoadState(string filename)
        {
            string path = Path.Combine(GetProjectDirectory(), filename);
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"Файл {path} не найден");
                    return;
                }
                var lines = File.ReadAllLines(path);
                for (int y = 0; y < board.Rows && y < lines.Length; y++)
                    for (int x = 0; x < board.Columns && x < lines[y].Length; x++)
                        board.Cells[x, y].IsAlive = lines[y][x] == '1';
                Console.WriteLine($"Состояние загружено из {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки состояния из {path}: {ex.Message}");
            }
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
            string patternsDir = Path.Combine(GetProjectDirectory(), "Patterns");
            if (!Directory.Exists(patternsDir))
            {
                Directory.CreateDirectory(patternsDir);
                CreateDefaultPatternFiles(patternsDir);
            }

            foreach (var file in Directory.GetFiles(patternsDir, "*.txt"))
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    if (lines.Length < 3) continue;

                    string name = lines[0];
                    string type = lines[1];
                    bool[,] shape = new bool[lines.Length - 2, lines[2].Length];

                    for (int i = 2; i < lines.Length; i++)
                    {
                        for (int j = 0; j < lines[i].Length; j++)
                        {
                            shape[i - 2, j] = lines[i][j] == '1';
                        }
                    }

                    patterns.Add(new Pattern
                    {
                        Name = name,
                        Type = type,
                        Shape = shape
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки паттерна из {file}: {ex.Message}");
                }
            }
        }

        static void CreateDefaultPatternFiles(string patternsDir)
        {
            File.WriteAllText(Path.Combine(patternsDir, "block.txt"),
                "Block\nStable\n11\n11");
            File.WriteAllText(Path.Combine(patternsDir, "beehive.txt"),
                "Beehive\nStable\n0110\n1001\n0110");
            File.WriteAllText(Path.Combine(patternsDir, "blinker.txt"),
                "Blinker\nPeriodic\n111");
            File.WriteAllText(Path.Combine(patternsDir, "glider.txt"),
                "Glider\nMoving\n010\n001\n111");
            File.WriteAllText(Path.Combine(patternsDir, "toad.txt"),
                "Toad\nPeriodic\n0111\n1110");
            File.WriteAllText(Path.Combine(patternsDir, "eater.txt"),
                "Eater\nEater\n1100\n1010\n0011");
            File.WriteAllText(Path.Combine(patternsDir, "ellipse.txt"),
                "Ellipse\nStable\n01110\n10001\n10001\n10001\n01110");
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
            string plotPath = Path.Combine(GetProjectDirectory(), "plot.png");
            Console.WriteLine($"Сохранение графика в: {plotPath}");
            int width = 800, height = 600;
            using (Bitmap bmp = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                Font font = new Font("Arial", 10);
                Pen[] pens = new Pen[]
                {
                    new Pen(Color.Blue, 2),
                    new Pen(Color.Red, 2),
                    new Pen(Color.Green, 2),
                    new Pen(Color.Purple, 2),
                    new Pen(Color.Orange, 2),
                    new Pen(Color.Cyan, 2),
                    new Pen(Color.Magenta, 2)
                };
                int margin = 50;
                int maxGen = 1000;
                int maxAlive = history.Any() && history.Max() > 0 ? history.Max() : 1;
                Console.WriteLine($"maxAlive: {maxAlive}");

                g.DrawLine(new Pen(Color.Black, 2), margin, height - margin, width - margin, height - margin);
                g.DrawLine(new Pen(Color.Black, 2), margin, height - margin, margin, margin);
                g.DrawString("Поколение", font, Brushes.Black, width / 2, height - margin + 10);
                g.DrawString("Живые клетки", font, Brushes.Black, 10, 10);

                for (int i = 0; i <= 5; i++)
                {
                    int x = margin + i * (width - 2 * margin) / 5;
                    int y = height - margin - i * (height - 2 * margin) / 5;
                    g.DrawString((maxGen * i / 5).ToString(), font, Brushes.Black, x, height - margin + 10);
                    g.DrawString((maxAlive * i / 5).ToString(), font, Brushes.Black, 10, y - 10);
                }

                Dictionary<double, List<int>> densityHistories = new Dictionary<double, List<int>>();
                Console.WriteLine($"Плотности для графика: {string.Join(", ", densityResults)}");
                foreach (var density in densityResults)
                {
                    string file = Path.Combine(GetProjectDirectory(), $"data_{density}.txt");
                    if (File.Exists(file))
                    {
                        try
                        {
                            var lines = File.ReadAllLines(file).Skip(1);
                            var history = lines.Select(line => int.Parse(line.Split('\t')[1])).ToList();
                            densityHistories[density] = history;
                            Console.WriteLine($"Плотность {density}: {history.Count} точек");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при чтении файла {file}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Файл {file} не найден");
                    }
                }

                int penIndex = 0;
                foreach (var kvp in densityHistories)
                {
                    var history = kvp.Value;
                    for (int i = 1; i < history.Count; i++)
                    {
                        int x1 = margin + (i - 1) * (width - 2 * margin) / maxGen;
                        int y1 = height - margin - history[i - 1] * (height - 2 * margin) / maxAlive;
                        int x2 = margin + i * (width - 2 * margin) / maxGen;
                        int y2 = height - margin - history[i] * (height - 2 * margin) / maxAlive;
                        g.DrawLine(pens[penIndex % pens.Length], x1, y1, x2, y2);
                    }
                    penIndex++;
                }

                for (int i = 0; i < densityHistories.Count; i++)
                {
                    g.DrawLine(pens[i % pens.Length], width - 150, 30 + i * 20, width - 100, 30 + i * 20);
                    g.DrawString($"Плотность {densityResults[i]}", font, Brushes.Black, width - 90, 25 + i * 20);
                }

                try
                {
                    bmp.Save(plotPath, ImageFormat.Png);
                    Console.WriteLine($"График успешно сохранен в {plotPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка сохранения графика в {plotPath}: {ex.Message}");
                }
            }
        }

        static void Reset()
        {
            var config = LoadConfig("settings.json");
            board = new Board(config.width, config.height, config.cellSize, config.liveDensity);
            generation = 0;
            history.Clear();
            string dataFile = Path.Combine(GetProjectDirectory(), "data.txt");
            File.WriteAllText(dataFile, "Generation\tAlive\n");
            InitializePatterns();
        }

        static void RunSimulation(double density, int runNumber)
        {
            var config = LoadConfig("settings.json");
            config.liveDensity = density;
            board = new Board(config.width, config.height, config.cellSize, config.liveDensity);
            generation = 0;
            history.Clear();

            string dataFile = Path.Combine(GetProjectDirectory(), $"data_{density}.txt");
            Console.WriteLine($"Запись данных в {dataFile}");
            if (runNumber == 1)
                File.WriteAllText(dataFile, "Generation\tAlive\n");

            while (generation < 500 && !IsStable())
            {
                Render();
                int alive = CountAlive();
                history.Add(alive);
                File.AppendAllText(dataFile, $"{generation}\t{alive}\n");
                board.Advance();
                generation++;
                Thread.Sleep(100);
            }

            string densityFile = Path.Combine(GetProjectDirectory(), $"density_{density}.txt");
            File.AppendAllText(densityFile, $"{runNumber} - запуск: количество поколений: {generation}\n");
            Console.WriteLine($"Плотность {density}, Запуск {runNumber}: Стабилизация на поколении {generation}");
        }

        static string GetProjectDirectory()
        {
            return @"C:\Users\HP\source\repos\mod-lab04-life\Life"; // Укажите свой путь
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
            Console.WriteLine($"Поколение: {generation}");
            Console.WriteLine($"Живые клетки: {CountAlive()}");
            Console.WriteLine($"Кластеры: {CountClusters()}");
            AnalyzePatterns();
            foreach (var kvp in patternCounts)
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            Console.WriteLine("S - Сохранить, L - Загрузить, R - Сбросить, Q - Выйти");
        }

        static void Main(string[] args)
        {
            Console.CursorVisible = false;
            Reset();

            string patternsDir = Path.Combine(GetProjectDirectory(), "Patterns");
            if (!Directory.Exists(patternsDir))
            {
                Directory.CreateDirectory(patternsDir);
                CreateDefaultPatternFiles(patternsDir);
            }

            double[] densities = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8 };
            const int runsPerDensity = 5;

            densityResults.Clear(); // Очищаем перед началом
            foreach (var density in densities)
            {
                string densityFile = Path.Combine(GetProjectDirectory(), $"density_{density}.txt");
                File.WriteAllText(densityFile, "");

                List<int> generations = new List<int>();
                for (int run = 1; run <= runsPerDensity; run++)
                {
                    Console.Clear();
                    Console.WriteLine($"Симуляция для плотности {density}, Запуск {run}");
                    densityResults.Add(density); // Добавляем плотность
                    RunSimulation(density, run);
                    generations.Add(generation);
                }
                double avgGenerations = generations.Average();
                File.AppendAllText(densityFile, $"Среднее количество поколений: {avgGenerations}\n");
            }

            Console.WriteLine($"Плотности для графика: {string.Join(", ", densityResults)}");
            GeneratePlot();

            //string[] patternFiles = { "Patterns/block.txt", "Patterns/ellipse.txt", "Patterns/glider.txt", "Patterns/blinker.txt", "Patterns/beehive.txt" };
            //foreach (var file in patternFiles)
            //{
            //    string fullPath = Path.Combine(GetProjectDirectory(), file);
            //    if (File.Exists(fullPath))
            //    {
            //        Console.Clear();
            //        Console.WriteLine($"Тестирование фигуры из {file}");
            //        Reset();
            //        LoadState(file);
            //        int maxGen = 100;
            //        for (int i = 0; i < maxGen && !IsStable(); i++)
            //        {
            //            Render();
            //            int alive = CountAlive();
            //            history.Add(alive);
            //            string dataFile = Path.Combine(GetProjectDirectory(), "data.txt");
            //            File.AppendAllText(dataFile, $"{generation}\t{alive}\n");
            //            board.Advance();
            //            generation++;
            //            Thread.Sleep(200);
            //        }
            //    }
            //}

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
                string dataFile = Path.Combine(GetProjectDirectory(), "data.txt");
                File.AppendAllText(dataFile, $"{generation}\t{alive}\n");

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