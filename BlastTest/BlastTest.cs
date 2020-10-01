// Copyright (c) 2012 James Telfer, released under the Apache 2.0 license:
// see http://www.apache.org/licenses/LICENSE-2.0.html.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Utils;
using Xunit;
using Shouldly;

namespace BlastTests {

	public class BlastTest {

		[Fact]
		public void basic_decompression_from_example() {
			// setup
			byte[] compressedInput = { 0x00, 0x04, 0x82, 0x24, 0x25, 0x8f, 0x80, 0x7f };
			byte[] expectedResult = Encoding.ASCII.GetBytes("AIAIAIAIAIAIA");

			var actualOutput = new MemoryStream();

			// test
			var sut = new Blast(new MemoryStream(compressedInput, writable: false), actualOutput);
			sut.Decompress();

            byte[] actualResult = actualOutput.ToArray();

			// assert
			actualResult.ShouldBe(expectedResult);
		}

		[Theory]
		[InlineData("test.log.blast")]
		[InlineData("lorem-ipsum.short.txt.blast")]
		// [InlineData("apache-license.html.blast")]
		public void decompress_test_files(string compressedSourceFile)
		{
			// setup
			var baseFolder = GetTestFileFolder();
			string expectedOutputFile = Path.GetFileNameWithoutExtension(compressedSourceFile);

			string compressedSourceFilePath = Path.Combine(baseFolder, compressedSourceFile);
			string expectedOutputFilePath = Path.Combine(baseFolder, expectedOutputFile);
			string actualOutputFilePath = Path.Combine(baseFolder, ".output", expectedOutputFile);

			Directory.CreateDirectory(Path.GetDirectoryName(actualOutputFilePath));

			using (var input = new FileStream(compressedSourceFilePath, FileMode.Open, FileAccess.Read))
			using (var output = new FileStream(actualOutputFilePath, FileMode.Create, FileAccess.Write))
			{
				// test
				var b = new Blast(input, output);
				b.Decompress();

				output.Flush();
			}

			// assert
			AssertFile(expectedOutputFilePath, actualOutputFilePath);
		}

		private void AssertFile(string expectedFileResult, string actualFileResult)
		{
			File.Exists(expectedFileResult).ShouldBeTrue("Expected file result must exist");
			File.Exists(actualFileResult).ShouldBeTrue("Actual file result must exist");

			var exp = new FileInfo(expectedFileResult);
			var act = new FileInfo(actualFileResult);

			exp.ShouldSatisfyAllConditions(
				() => exp.Length.ShouldBe(act.Length),
				() => File.ReadAllText(actualFileResult).ShouldBe(File.ReadAllText(expectedFileResult))
			);
		}

		private static string GetTestFileFolder()
		{
			var projDir = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location).Parent.Parent.FullName;
			var candidate = Path.Combine(projDir, "test-files");

			if (!Directory.Exists(candidate))
			{
				candidate =
					Path.Combine(projDir, "../test-files");
			}

			if (!Directory.Exists(candidate))
			{
				candidate =
					Path.Combine(Environment.CurrentDirectory, "test-files");
			}

			Assert.True(Directory.Exists(candidate), $"Input file location must exist relative to '{projDir}' or '{Environment.CurrentDirectory}'");

			return candidate;
		}
	}
}
