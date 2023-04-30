namespace DeduplicateNugetTool
{
    using System;
    using Microsoft.Extensions.FileProviders;
    public class Program
	{
        
		public static int Main()
		{
            var tool = new Tool(
				new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                    Directory.GetCurrentDirectory()
                )
			);
            tool.Run();
            Console.WriteLine("hello");
			return 0;
		}

    }


}


