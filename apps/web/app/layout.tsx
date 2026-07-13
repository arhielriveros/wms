import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "WMS · Control de almacén",
  description: "Consola operacional del WMS modular",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="es">
      <body>{children}</body>
    </html>
  );
}
