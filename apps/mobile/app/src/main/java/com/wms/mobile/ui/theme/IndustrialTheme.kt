package com.wms.mobile.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

val SurfaceWarm = Color(0xFFF7F5EF)
val Ink = Color(0xFF17211B)
val Primary = Color(0xFF075E54)
val Info = Color(0xFF1463D6)
val Success = Color(0xFF137A3D)
val Warning = Color(0xFFA85C00)
val Danger = Color(0xFFB42318)
val Steel = Color(0xFF52625A)

private val LightColors = lightColorScheme(
    primary = Primary,
    onPrimary = Color.White,
    secondary = Info,
    background = SurfaceWarm,
    onBackground = Ink,
    surface = Color.White,
    onSurface = Ink,
    error = Danger,
    outline = Steel,
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFF65D1C2),
    secondary = Color(0xFF8AB4F8),
    background = Color(0xFF101713),
    surface = Color(0xFF19231E),
    error = Color(0xFFFFB4AB),
)

private val IndustrialTypography = Typography(
    headlineLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontSize = 30.sp,
        lineHeight = 34.sp,
        fontWeight = FontWeight.Black,
        letterSpacing = (-0.5).sp,
    ),
    titleLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontSize = 22.sp,
        lineHeight = 28.sp,
        fontWeight = FontWeight.Bold,
    ),
    titleMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontSize = 17.sp,
        lineHeight = 22.sp,
        fontWeight = FontWeight.Bold,
    ),
    bodyLarge = TextStyle(fontSize = 17.sp, lineHeight = 24.sp),
    bodyMedium = TextStyle(fontSize = 15.sp, lineHeight = 21.sp),
    labelLarge = TextStyle(fontSize = 16.sp, fontWeight = FontWeight.Bold),
)

@Composable
fun WmsTheme(darkTheme: Boolean = false, content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        typography = IndustrialTypography,
        content = content,
    )
}
