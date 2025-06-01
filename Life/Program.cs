using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

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

    class Program
    {
        static Board board;
        static int generation = 0;
        static List<int> history = new List<int>();
        const int stableLimit = 5;

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
            for (int y = 0; y < board.Rows; y++)
                for (int x = 0; x < board.Columns; x++)
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

        static void Reset()
        {
            var config = LoadConfig("settings.json");
            board = new Board(config.width, config.height, config.cellSize, config.liveDensity);
            generation = 0;
            history.Clear();
            File.WriteAllText("data.txt", "Generation\tAlive\n");
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
            Console.WriteLine("S - Save, L - Load, R - Reset, Q - Quit");
        }

        static void Main(string[] args)
        {
            Console.CursorVisible = false;
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