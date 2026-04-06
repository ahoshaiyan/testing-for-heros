using Minio;
using Minio.DataModel.Args;

namespace FoodApp.Api.Services;

public class StorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly string _publicBaseUrl;

    public StorageService(IMinioClient minioClient, IConfiguration configuration)
    {
        _minioClient = minioClient;
        _bucketName = configuration["Storage:BucketName"] ?? "foodapp";
        _publicBaseUrl = configuration["Storage:PublicBaseUrl"] ?? string.Empty;
    }

    public async Task<string> UploadAsync(IFormFile file, string folder)
    {
        var extension = Path.GetExtension(file.FileName);
        var objectName = $"{folder}/{Guid.NewGuid()}{extension}";

        using var stream = file.OpenReadStream();

        var putArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(file.Length)
            .WithContentType(file.ContentType);

        await _minioClient.PutObjectAsync(putArgs);

        var baseUrl = _publicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{_bucketName}/{objectName}";
    }

    public async Task EnsureBucketExistsAsync()
    {
        var existsArgs = new BucketExistsArgs().WithBucket(_bucketName);
        bool exists = await _minioClient.BucketExistsAsync(existsArgs);

        if (!exists)
        {
            var makeArgs = new MakeBucketArgs().WithBucket(_bucketName);
            await _minioClient.MakeBucketAsync(makeArgs);
        }

        // Set public read policy
        var policy = $$"""
        {
            "Version": "2012-10-17",
            "Statement": [
                {
                    "Effect": "Allow",
                    "Principal": {"AWS": ["*"]},
                    "Action": ["s3:GetObject"],
                    "Resource": ["arn:aws:s3:::{{_bucketName}}/*"]
                }
            ]
        }
        """;

        var policyArgs = new SetPolicyArgs()
            .WithBucket(_bucketName)
            .WithPolicy(policy);

        await _minioClient.SetPolicyAsync(policyArgs);
    }
}
