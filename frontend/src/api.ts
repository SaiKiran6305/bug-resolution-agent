let accessKey = "";
export function setAccessKey(value: string) {
  accessKey = value;
}
export async function api<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch("/api" + path, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      "X-Api-Key": accessKey,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const text = await response.text();
    let message = `Request failed (${response.status})`;
    try {
      const data = JSON.parse(text);
      message = data.error ?? data.detail ?? message;
    } catch {
      /* Keep bounded generic message. */
    }
    throw new Error(message);
  }
  return response.json() as Promise<T>;
}
export async function downloadPatch(id: string) {
  const response = await fetch(`/api/investigations/${id}/patch`, {
    headers: { "X-Api-Key": accessKey },
  });
  if (!response.ok) throw new Error("Patch download failed.");
  const url = URL.createObjectURL(await response.blob());
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${id}.patch`;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
