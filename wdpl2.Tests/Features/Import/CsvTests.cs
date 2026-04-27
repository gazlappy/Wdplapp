using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests
{
    public class CsvTests
    {
        [Fact]
        public void Read_EmptyStream_ReturnsEmptyList()
        {
            // Arrange
            using var stream = new MemoryStream();

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Read_HeaderOnlyNoDataRows_ReturnsEmptyList()
        {
            // Arrange
            var csv = "Name,Age,City";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Read_CommaSeparatedData_ReturnsCorrectRows()
        {
            // Arrange
            var csv = "Name,Age,City\nJohn,30,NYC\nJane,25,LA";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("John", result[0]["Name"]);
            Assert.Equal("30", result[0]["Age"]);
            Assert.Equal("NYC", result[0]["City"]);
            Assert.Equal("Jane", result[1]["Name"]);
            Assert.Equal("25", result[1]["Age"]);
            Assert.Equal("LA", result[1]["City"]);
        }

        [Fact]
        public void Read_SemicolonDelimiter_ReturnsCorrectRows()
        {
            // Arrange
            var csv = "Name;Age;City\nBob;40;Paris";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Single(result);
            Assert.Equal("Bob", result[0]["Name"]);
            Assert.Equal("40", result[0]["Age"]);
            Assert.Equal("Paris", result[0]["City"]);
        }

        [Fact]
        public void Read_TabDelimiter_ReturnsCorrectRows()
        {
            // Arrange
            var csv = "Name\tAge\tCity\nAlice\t35\tTokyo";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Single(result);
            Assert.Equal("Alice", result[0]["Name"]);
            Assert.Equal("35", result[0]["Age"]);
            Assert.Equal("Tokyo", result[0]["City"]);
        }

        [Fact]
        public void Read_PipeDelimiter_ReturnsCorrectRows()
        {
            // Arrange
            var csv = "Name|Age|City\nCharlie|28|Berlin";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Single(result);
            Assert.Equal("Charlie", result[0]["Name"]);
            Assert.Equal("28", result[0]["Age"]);
            Assert.Equal("Berlin", result[0]["City"]);
        }

        [Fact]
        public void Read_EmptyLinesSkipped_ReturnsOnlyNonEmptyRows()
        {
            // Arrange
            var csv = "Name,Age\nJohn,30\n\nJane,25\n  \n";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("John", result[0]["Name"]);
            Assert.Equal("Jane", result[1]["Name"]);
        }

        [Fact]
        public void Read_FewerFieldsThanHeaders_FillsWithEmptyString()
        {
            // Arrange
            var csv = "Name,Age,City\nJohn,30\nJane";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("John", result[0]["Name"]);
            Assert.Equal("30", result[0]["Age"]);
            Assert.Equal("", result[0]["City"]);
            Assert.Equal("Jane", result[1]["Name"]);
            Assert.Equal("", result[1]["Age"]);
            Assert.Equal("", result[1]["City"]);
        }

        [Fact]
        public void Read_HeadersWithWhitespace_TrimsHeaders()
        {
            // Arrange
            var csv = " Name , Age , City \nJohn,30,NYC";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Single(result);
            Assert.Equal("John", result[0]["Name"]);
            Assert.Equal("30", result[0]["Age"]);
            Assert.Equal("NYC", result[0]["City"]);
        }

        [Fact]
        public void Read_CaseInsensitiveDictionary_AllowsCaseInsensitiveAccess()
        {
            // Arrange
            var csv = "Name,Age\nJohn,30";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream);

            // Assert
            Assert.Single(result);
            Assert.Equal("John", result[0]["name"]);
            Assert.Equal("30", result[0]["AGE"]);
            Assert.Equal("30", result[0]["age"]);
        }

        [Fact]
        public void Read_CustomEncoding_ReadsCorrectly()
        {
            // Arrange
            var csv = "Name,City\nJohn,Zürich";
            using var stream = CreateStream(csv, Encoding.UTF8);

            // Act
            var result = Csv.Read(stream, Encoding.UTF8);

            // Assert
            Assert.Single(result);
            Assert.Equal("John", result[0]["Name"]);
            Assert.Equal("Zürich", result[0]["City"]);
        }

        [Fact]
        public void Read_NullEncoding_DefaultsToUtf8()
        {
            // Arrange
            var csv = "Name,Age\nJohn,30";
            using var stream = CreateStream(csv);

            // Act
            var result = Csv.Read(stream, null);

            // Assert
            Assert.Single(result);
            Assert.Equal("John", result[0]["Name"]);
            Assert.Equal("30", result[0]["Age"]);
        }

        [Fact]
        public void ToCsv_EmptyEnumerable_ReturnsHeaderOnly()
        {
            // Arrange
            var rows = new List<Person>();

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name),
                ("Age", p => p.Age));

            // Assert
            Assert.Equal("Name,Age\r\n", result);
        }

        [Fact]
        public void ToCsv_SingleRow_ReturnsHeaderAndRow()
        {
            // Arrange
            var rows = new List<Person> { new Person { Name = "John", Age = 30 } };

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name),
                ("Age", p => p.Age));

            // Assert
            Assert.Equal("Name,Age\r\nJohn,30\r\n", result);
        }

        [Fact]
        public void ToCsv_MultipleRows_ReturnsAllRows()
        {
            // Arrange
            var rows = new List<Person>
            {
                new Person { Name = "John", Age = 30 },
                new Person { Name = "Jane", Age = 25 }
            };

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name),
                ("Age", p => p.Age));

            // Assert
            Assert.Equal("Name,Age\r\nJohn,30\r\nJane,25\r\n", result);
        }

        [Fact]
        public void ToCsv_ValueWithComma_EscapesWithQuotes()
        {
            // Arrange
            var rows = new List<Person> { new Person { Name = "Doe, John", Age = 30 } };

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name));

            // Assert
            Assert.Contains("\"Doe, John\"", result);
        }

        [Fact]
        public void ToCsv_ValueWithQuotes_EscapesQuotes()
        {
            // Arrange
            var rows = new List<Person> { new Person { Name = "John \"Johnny\" Doe", Age = 30 } };

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name));

            // Assert
            Assert.Contains("\"John \"\"Johnny\"\" Doe\"", result);
        }

        [Fact]
        public void ToCsv_ValueWithNewline_EscapesWithQuotes()
        {
            // Arrange
            var rows = new List<Person> { new Person { Name = "John\nDoe", Age = 30 } };

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name));

            // Assert
            Assert.Contains("\"John\nDoe\"", result);
        }

        [Fact]
        public void ToCsv_NullValue_ReturnsEmptyString()
        {
            // Arrange
            var rows = new List<Person> { new Person { Name = null, Age = 30 } };

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name),
                ("Age", p => p.Age));

            // Assert
            Assert.Equal("Name,Age\r\n,30\r\n", result);
        }

        [Fact]
        public void ToCsv_HeaderWithComma_EscapesHeaderWithQuotes()
        {
            // Arrange
            var rows = new List<Person> { new Person { Name = "John", Age = 30 } };

            // Act
            var result = Csv.ToCsv(rows,
                ("Full Name, Last First", p => p.Name));

            // Assert
            Assert.StartsWith("\"Full Name, Last First\"\r\n", result);
        }

        [Fact]
        public void ToCsv_MultipleColumnsWithVaryingTypes_FormatsCorrectly()
        {
            // Arrange
            var rows = new List<Person>
            {
                new Person { Name = "John", Age = 30 },
                new Person { Name = "Jane", Age = 25 }
            };

            // Act
            var result = Csv.ToCsv(rows,
                ("Name", p => p.Name),
                ("Age", p => p.Age),
                ("Is Adult", p => p.Age >= 18));

            // Assert
            Assert.Equal("Name,Age,Is Adult\r\nJohn,30,True\r\nJane,25,True\r\n", result);
        }

        private static MemoryStream CreateStream(string content, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = encoding.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private class Person
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }
    }
}
