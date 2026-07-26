package com.audiobridge.app.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

private val DarkColorScheme = darkColorScheme(
    primary = AccentPrimary,
    secondary = AccentSecondary,
    tertiary = AccentWarning,
    surface = SurfaceDark,
    onSurface = TextPrimary,
    onSurfaceVariant = TextSecondary,
    surfaceVariant = SurfaceElevatedDark,
)

private val LightColorScheme = lightColorScheme(
    primary = AccentPrimary,
    secondary = AccentSecondary,
    tertiary = AccentWarning,
    surface = SurfaceLight,
    onSurface = TextPrimaryLight,
    onSurfaceVariant = TextSecondary,
)

@Composable
fun AudioBridgeTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    val colorScheme = if (darkTheme) DarkColorScheme else LightColorScheme

    MaterialTheme(
        colorScheme = colorScheme,
        shapes = AudioBridgeShapes,
        typography = AudioBridgeTypography,
        content = content
    )
}

@Composable
fun AudioBridgeSurface(
    modifier: Modifier = Modifier,
    content: @Composable () -> Unit
) {
    Surface(
        modifier = modifier,
        color = MaterialTheme.colorScheme.surface,
        shape = MaterialTheme.shapes.large,
        tonalElevation = 2.dp,
        content = content
    )
}
