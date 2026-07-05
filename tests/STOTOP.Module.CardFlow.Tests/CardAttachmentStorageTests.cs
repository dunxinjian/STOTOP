using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using STOTOP.Module.CardFlow.Services;
using Xunit;

// 注：STOTOP.Module.Task / STOTOP.Module.System 命名空间遮蔽 System.Threading.Tasks.Task，
// 方法返回类型用 global:: 全限定（别名 using 在 file-scoped namespace 下不稳定）。

namespace STOTOP.Module.CardFlow.Tests;

public class CardAttachmentStorageTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task SaveAndResolve_RoundTrip_And_CrossOrgAndTraversalRejected()
    {
        var temp = Path.Combine(Path.GetTempPath(), "cf-att-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var env = new FakeWebHostEnvironment { ContentRootPath = temp };
            var file = MakeFile("报销票据.pdf", "application/pdf", "hello");

            var saved = await CardAttachmentStorage.SaveAsync(env, file, orgId: 5);
            Assert.StartsWith("5/", saved.StorageKey);
            Assert.Equal("报销票据.pdf", saved.OriginalName);

            // 同组织解析成功且文件真实落盘
            Assert.True(CardAttachmentStorage.TryResolvePath(env, 5, saved.StorageKey, out var path));
            Assert.True(File.Exists(path));

            // 跨组织读取拒绝（首段 orgId 不匹配）
            Assert.False(CardAttachmentStorage.TryResolvePath(env, 6, saved.StorageKey, out _));

            // 目录穿越拒绝
            Assert.False(CardAttachmentStorage.TryResolvePath(env, 5, "5/../../../etc/passwd", out _));
            Assert.False(CardAttachmentStorage.TryResolvePath(env, 5, "../6/2026-07/x.pdf", out _));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Save_DisallowedExtension_Throws()
    {
        var env = new FakeWebHostEnvironment { ContentRootPath = Path.GetTempPath() };
        var file = MakeFile("evil.exe", "application/octet-stream", "x");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CardAttachmentStorage.SaveAsync(env, file, 5));
    }

    private static IFormFile MakeFile(string name, string contentType, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
