using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using cli_life;

namespace LifeTests
{
    public class LifeGameTests : IDisposable
    {
        private Board board;

        public LifeGameTests()
        {
            // Инициализация перед каждым тестом
            board = new Board(10, 10, 1, 0.0);
            // Сбрасываем статические поля Program
            ResetProgramState();
        }

        public void Dispose()
        {
            // Очистка после каждого теста
            ResetProgramState();
            if (File.Exists("test_settings.json"))
                File.Delete("test_settings.json");
            if (File.Exists("test_save.txt"))
                File.Delete("test_save.txt");
            if (File.Exists("test_load.txt"))
                File.Delete("test_load.txt");
        }

        private void ResetProgramState()
        {
            // Используем рефлексию для очистки статических полей
            var historyField = typeof(Program).GetField("history", BindingFlags.NonPublic | BindingFlags.Static);
            historyField.SetValue(null, new System.Collections.Generic.List<int>());

            var patternCountsField = typeof(Program).GetField("patternCounts", BindingFlags.NonPublic | BindingFlags.Static);
            patternCountsField.SetValue(null, new System.Collections.Generic.Dictionary<string, int>());

            var patternsField = typeof(Program).GetField("patterns", BindingFlags.NonPublic | BindingFlags.Static);
            patternsField.SetValue(null, new System.Collections.Generic.List<Pattern>());

            var stableGenerationsField = typeof(Program).GetField("stableGenerations", BindingFlags.NonPublic | BindingFlags.Static);
            stableGenerationsField.SetValue(null, new System.Collections.Generic.List<int>());

            var densityResultsField = typeof(Program).GetField("densityResults", BindingFlags.NonPublic | BindingFlags.Static);
            densityResultsField.SetValue(null, new System.Collections.Generic.List<double>());

            var boardField = typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static);
            boardField.SetValue(null, null);

            var generationField = typeof(Program).GetField("generation", BindingFlags.NonPublic | BindingFlags.Static);
            generationField.SetValue(null, 0);
        }

        [Fact]
        public void TestBoardInitialization()
        {
            board = new Board(50, 20, 1, 0.0);
            Assert.Equal(50, board.Columns);
            Assert.Equal(20, board.Rows);
            Assert.All(board.Cells.Cast<Cell>(), c => Assert.False(c.IsAlive));
        }

        [Fact]
        public void TestCellNeighbors()
        {
            var cell = board.Cells[5, 5];
            Assert.Equal(8, cell.neighbors.Count);
        }

        [Fact]
        public void TestRandomize()
        {
            board = new Board(10, 10, 1, 1.0);
            board.Randomize(1.0);
            Assert.All(board.Cells.Cast<Cell>(), c => Assert.True(c.IsAlive));
        }

        [Fact]
        public void TestAdvanceLonelyCell()
        {
            board.Cells[1, 1].IsAlive = true;
            board.Advance();
            Assert.False(board.Cells[1, 1].IsAlive);
        }

        [Fact]
        public void TestAdvanceStableBlock()
        {
            board = new Board(4, 4, 1, 0.0);
            board.Cells[1, 1].IsAlive = true;
            board.Cells[1, 2].IsAlive = true;
            board.Cells[2, 1].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            board.Advance();
            Assert.True(board.Cells[1, 1].IsAlive);
            Assert.True(board.Cells[1, 2].IsAlive);
            Assert.True(board.Cells[2, 1].IsAlive);
            Assert.True(board.Cells[2, 2].IsAlive);
        }

        [Fact]
        public void TestAdvanceBirth()
        {
            board = new Board(3, 3, 1, 0.0);
            board.Cells[0, 0].IsAlive = true;
            board.Cells[0, 1].IsAlive = true;
            board.Cells[1, 0].IsAlive = true;
            board.Advance();
            Assert.True(board.Cells[1, 1].IsAlive);
        }

        [Fact]
        public void TestAdvanceOvercrowding()
        {
            board = new Board(3, 3, 1, 0.0);
            board.Cells[1, 1].IsAlive = true;
            board.Cells[0, 0].IsAlive = true;
            board.Cells[0, 1].IsAlive = true;
            board.Cells[1, 0].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            board.Advance();
            Assert.False(board.Cells[1, 1].IsAlive);
        }

        [Fact]
        public void TestLoadConfig()
        {
            File.WriteAllText("test_settings.json", "{\"width\":100,\"height\":50,\"cellSize\":2,\"liveDensity\":0.5}");
            var config = (Config)typeof(Program).GetMethod("LoadConfig", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { "test_settings.json" });
            Assert.Equal(100, config.width);
            Assert.Equal(50, config.height);
            Assert.Equal(2, config.cellSize);
            Assert.Equal(0.5, config.liveDensity);
        }

        [Fact]
        public void TestSaveState()
        {
            board = new Board(3, 3, 1, 0.0);
            typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, board);
            board.Cells[1, 1].IsAlive = true;
            typeof(Program).GetMethod("SaveState", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { "test_save.txt" });
            var lines = File.ReadAllLines("test_save.txt");
            Assert.Equal("000", lines[0]);
            Assert.Equal("010", lines[1]);
            Assert.Equal("000", lines[2]);
        }

        [Fact]
        public void TestLoadState()
        {
            File.WriteAllText("test_load.txt", "000\n010\n000");
            board = new Board(3, 3, 1, 0.0);
            typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, board);
            typeof(Program).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { "test_load.txt" });
            Assert.True(board.Cells[1, 1].IsAlive);
        }

        [Fact]
        public void TestCountAlive()
        {
            board = new Board(3, 3, 1, 0.0);
            board.Cells[0, 0].IsAlive = true;
            board.Cells[1, 1].IsAlive = true;
            typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, board);
            var count = (int)typeof(Program).GetMethod("CountAlive", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            Assert.Equal(2, count);
        }

        [Fact]
        public void TestIsStable()
        {
            board = new Board(4, 4, 1, 0.0);
            board.Cells[1, 1].IsAlive = true;
            board.Cells[1, 2].IsAlive = true;
            board.Cells[2, 1].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, board);
            var historyField = typeof(Program).GetField("history", BindingFlags.NonPublic | BindingFlags.Static);
            var history = (System.Collections.Generic.List<int>)historyField.GetValue(null);
            for (int i = 0; i < 5; i++)
                history.Add(4);
            var isStable = (bool)typeof(Program).GetMethod("IsStable", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            Assert.True(isStable);
        }

        [Fact]
        public void TestPatternBlock()
        {
            board = new Board(4, 4, 1, 0.0);
            board.Cells[1, 1].IsAlive = true;
            board.Cells[1, 2].IsAlive = true;
            board.Cells[2, 1].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, board);
            typeof(Program).GetMethod("InitializePatterns", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            typeof(Program).GetMethod("AnalyzePatterns", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            var patternCounts = (System.Collections.Generic.Dictionary<string, int>)typeof(Program)
                .GetField("patternCounts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.Equal(1, patternCounts["Block"]);
        }

        [Fact]
        public void TestCountClusters()
        {
            board = new Board(5, 5, 1, 0.0);
            board.Cells[0, 0].IsAlive = true;
            board.Cells[0, 1].IsAlive = true;
            board.Cells[3, 3].IsAlive = true;
            typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, board);
            var clusters = (int)typeof(Program).GetMethod("CountClusters", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            Assert.Equal(2, clusters);
        }

        [Fact]
        public void TestGliderMovement()
        {
            board = new Board(5, 5, 1, 0.0);
            board.Cells[1, 0].IsAlive = true;
            board.Cells[2, 1].IsAlive = true;
            board.Cells[0, 2].IsAlive = true;
            board.Cells[1, 2].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            board.Advance();
            Assert.True(board.Cells[2, 1].IsAlive);
            Assert.True(board.Cells[1, 2].IsAlive);
            Assert.True(board.Cells[2, 2].IsAlive);
            Assert.True(board.Cells[0, 1].IsAlive);
        }

        [Fact]
        public void TestBlinkerOscillation()
        {
            board = new Board(5, 5, 1, 0.0);
            board.Cells[1, 2].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            board.Cells[3, 2].IsAlive = true;
            board.Advance();
            Assert.True(board.Cells[2, 1].IsAlive);
            Assert.True(board.Cells[2, 2].IsAlive);
            Assert.True(board.Cells[2, 3].IsAlive);
        }

        [Fact]
        public void TestPatternBeehive()
        {
            board = new Board(6, 5, 1, 0.0);
            board.Cells[2, 1].IsAlive = true;
            board.Cells[3, 1].IsAlive = true;
            board.Cells[1, 2].IsAlive = true;
            board.Cells[4, 2].IsAlive = true;
            board.Cells[2, 3].IsAlive = true;
            board.Cells[3, 3].IsAlive = true;
            typeof(Program).GetField("board", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, board);
            typeof(Program).GetMethod("InitializePatterns", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            typeof(Program).GetMethod("AnalyzePatterns", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            var patternCounts = (System.Collections.Generic.Dictionary<string, int>)typeof(Program)
                .GetField("patternCounts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.Equal(1, patternCounts["Beehive"]);
        }
    }
}