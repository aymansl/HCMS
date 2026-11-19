namespace HCMS4.Shared.Common
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();

        public static ServiceResult Ok(string? message = null)
        {
            return new ServiceResult { Success = true, Message = message ?? string.Empty };
        }

        public static ServiceResult Fail(string message, List<string>? errors = null)
        {
            return new ServiceResult
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }

    public class ServiceResult<T> : ServiceResult
    {
        /// <summary>The result value; only meaningful when <see cref="ServiceResult.Success"/> is true.</summary>
        public T Data { get; set; } = default!;

        public static ServiceResult<T> Ok(T data, string? message = null)
        {
            return new ServiceResult<T> { Success = true, Message = message ?? string.Empty, Data = data };
        }

        public static new ServiceResult<T> Fail(string message, List<string>? errors = null)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }
}
