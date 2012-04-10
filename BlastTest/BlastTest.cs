using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils;

namespace BlastTests {

    [TestClass]
    public class BlastTest {

        [TestMethod]
        public void basic_decompression_from_example() {
            // setup
            byte[] input = { 0x00, 0x04, 0x82, 0x24, 0x25, 0x8f, 0x80, 0x7f };
            byte[] expected = Encoding.ASCII.GetBytes("AIAIAIAIAIAIA");

            var outp = new MemoryStream();

            // test
            var b = new Blast(new MemoryStream(input, writable: false), outp);
            b.Decompress();
            Console.WriteLine(Encoding.ASCII.GetString(outp.ToArray()));

            // assert
            CollectionAssert.AreEqual(expected, outp.ToArray());
        }

		[TestMethod]
		public void decompress_text_file()
		{
			// setup
			using (var input = new FileStream(@"D:\Source\ref\blast\BlastTest\test-files\test.bin", FileMode.Open, FileAccess.Read))
			using (var output = new FileStream(@"D:\Source\ref\blast\BlastTest\test-files\test.decomp.txt", FileMode.Create, FileAccess.Write))
			{

				// test
				var b = new Blast(input, output);
				b.Decompress();
			}

			// assert
		}

		[TestMethod]
		public void decompress_binary_file()
		{
			// setup
			using (var input = new FileStream(@"D:\Source\ref\blast\BlastTest\test-files\blast.msg.cmp", FileMode.Open, FileAccess.Read))
			using (var output = new FileStream(@"D:\Source\ref\blast\BlastTest\test-files\blast.decomp.msg", FileMode.Create, FileAccess.Write))
			{

				// test
				var b = new Blast(input, output);
				b.Decompress();
			}

			// assert
		}
	}
}
