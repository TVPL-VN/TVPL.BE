// Read-only rollout check. No authentication, writes, or migration execution.
const base = (process.argv[2] ?? 'https://api.tvphapluat.com.vn').replace(/\/$/, '');
const required = {
  '/api/legal-documents/catalog': 'get',
  '/api/admin/legal-documents/catalog/{kind}': 'post',
};
try {
  const response = await fetch(`${base}/swagger/v1/swagger.json`, { signal: AbortSignal.timeout(15000) });
  if (!response.ok) throw new Error(`Swagger trả HTTP ${response.status}; chưa xác minh được bản deploy.`);
  const schema = await response.json();
  const missing = Object.entries(required).filter(([path, method]) => !schema.paths?.[path]?.[method]);
  if (missing.length) {
    throw new Error(`Backend đang chạy thiếu API: ${missing.map(([path]) => path).join(', ')}. Deploy commit e594d628 hoặc mới hơn từ TVPL-VN/TVPL.BE, branch main.`);
  }
  const catalog = await fetch(`${base}/api/legal-documents/catalog`, { signal: AbortSignal.timeout(15000) });
  if (!catalog.ok) throw new Error(`API đã có nhưng danh mục trả HTTP ${catalog.status}. Kiểm tra migration và log backend.`);
  const data = await catalog.json();
  if (!Array.isArray(data.fields) || !Array.isArray(data.authorities)) throw new Error('Danh mục trả sai cấu trúc.');
  console.log('API module luật và database danh mục đã sẵn sàng.');
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
