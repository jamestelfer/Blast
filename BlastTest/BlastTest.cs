// Copyright (c) 2012 James Telfer, released under the Apache 2.0 license: 
// see http://www.apache.org/licenses/LICENSE-2.0.html.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Blast;
using Xunit;

namespace Blast.Test {

    public class BlastTest {

        [Fact]
        public void basic_decompression_from_example() {
            // setup
            byte[] input = { 0x00, 0x04, 0x82, 0x24, 0x25, 0x8f, 0x80, 0x7f };
            byte[] expected = Encoding.ASCII.GetBytes("AIAIAIAIAIAIA");

            var outp = new MemoryStream();

            // test
            var b = new BlastDecoder(new MemoryStream(input, writable: false), outp);
            b.Decompress();
            Console.WriteLine(Encoding.ASCII.GetString(outp.ToArray()));

            // assert
            Assert.Equal(expected, outp.ToArray());
        }

		[Fact]
		public void decompress_text_file()
		{
			// setup
			var baseFolder = GetTestFileFolder();

			using (var input = new FileStream(Path.Combine(baseFolder, "test.bin"), FileMode.Open, FileAccess.Read))
			using (var output = new FileStream(Path.Combine(baseFolder, "test.decomp.log"), FileMode.Create, FileAccess.Write))
			{

				// test
				var b = new BlastDecoder(input, output);
				b.Decompress();
			}

			// assert
		}

		[Fact]
		public void decompress_large_text_file()
		{
			// setup
			var baseFolder = GetTestFileFolder();
            var resultFile = Path.Combine(baseFolder, "large.decomp.log");

			using (var input = new FileStream(Path.Combine(baseFolder, "large.log.cmp"), FileMode.Open, FileAccess.Read))
			using (var output = new FileStream(resultFile, FileMode.Create, FileAccess.Write))
			{

				// test
				var b = new BlastDecoder(input, output);
				b.Decompress();
			}

			// assert
            AssertFile(Path.Combine(baseFolder, "large.log"), resultFile);
        }

		[Fact]
		public void decompress_binary_file()
		{
			// setup
            var baseFolder = GetTestFileFolder();

            var resultFile = Path.Combine(baseFolder, "blast.decomp.msg");

            using (var input = new FileStream(Path.Combine(baseFolder, "blast.msg.cmp"), FileMode.Open, FileAccess.Read))
			using (var output = new FileStream(resultFile, FileMode.Create, FileAccess.Write))
			{

				// test
				var b = new BlastDecoder(input, output);
				b.Decompress();
			}

			// assert
            AssertFile(Path.Combine(baseFolder, "blast.msg"), resultFile);
		}

        private void AssertFile(string expectedFileResult, string actualFileResult)
        {
            Assert.True(File.Exists(expectedFileResult), "Expected file result must exist");
            Assert.True(File.Exists(actualFileResult), "Actual file result must exist");

            var exp = new FileInfo(expectedFileResult);
            var act = new FileInfo(actualFileResult);
            
            Assert.Equal(exp.Length, act.Length);

            using (var expStream = new FileStream(expectedFileResult, FileMode.Open, FileAccess.Read))
            using (var actStream = new FileStream(actualFileResult, FileMode.Open, FileAccess.Read))
            {
                Assert.True(StreamsContentsAreEqual(expStream, actStream), "Files differ");
            }
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

        // http://stackoverflow.com/questions/968935/c-sharp-binary-file-compare
        private static bool StreamsContentsAreEqual(Stream stream1, Stream stream2)
        {
            const int bufferSize = 2048 * 2;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];

            while (true)
            {
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);

                if (count1 != count2)
                {
                    return false;
                }

                if (count1 == 0)
                {
                    return true;
                }

                int iterations = (int)Math.Ceiling((double)count1 / sizeof(Int64));
                for (int i = 0; i < iterations; i++)
                {
                    if (BitConverter.ToInt64(buffer1, i * sizeof(Int64)) != BitConverter.ToInt64(buffer2, i * sizeof(Int64)))
                    {
                        return false;
                    }
                }
            }
        }
    }
}
