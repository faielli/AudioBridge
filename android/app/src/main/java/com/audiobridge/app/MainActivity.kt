package com.audiobridge.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.audiobridge.app.ui.screen.FavouritesScreen
import com.audiobridge.app.ui.screen.MainScreen
import com.audiobridge.app.ui.screen.SettingsScreen
import com.audiobridge.app.ui.theme.AccentPrimary
import com.audiobridge.app.ui.theme.AudioBridgeSurface
import com.audiobridge.app.ui.theme.AudioBridgeTheme
import com.audiobridge.app.ui.theme.SurfaceDark
import com.audiobridge.app.ui.theme.TextSecondary
import com.audiobridge.app.viewmodel.MainViewModel

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            AudioBridgeTheme {
                AudioBridgeSurface {
                    val vm: MainViewModel = viewModel()
                    AppNavigation(vm)
                }
            }
        }
    }
}

private data class NavItem(
    val label: String,
    val icon: ImageVector,
    val content: @Composable (MainViewModel) -> Unit
)

@Composable
private fun AppNavigation(viewModel: MainViewModel) {
    val items = listOf(
        NavItem("Connetti", Icons.Default.Wifi) { vm -> MainScreen(vm) },
        NavItem("Preferiti", Icons.Default.Star) { vm -> FavouritesScreen(vm) },
        NavItem("Impostazioni", Icons.Default.Settings) { vm -> SettingsScreen(vm) }
    )

    var selectedIndex by rememberSaveable { mutableIntStateOf(0) }

    Scaffold(
        bottomBar = {
            NavigationBar(
                containerColor = SurfaceDark,
                tonalElevation = 0.dp
            ) {
                items.forEachIndexed { index, item ->
                    NavigationBarItem(
                        selected = selectedIndex == index,
                        onClick = { selectedIndex = index },
                        icon = {
                            Icon(
                                imageVector = item.icon,
                                contentDescription = item.label
                            )
                        },
                        label = { Text(item.label) },
                        colors = NavigationBarItemDefaults.colors(
                            selectedIconColor = AccentPrimary,
                            selectedTextColor = AccentPrimary,
                            unselectedIconColor = TextSecondary,
                            unselectedTextColor = TextSecondary,
                            indicatorColor = AccentPrimary.copy(alpha = 0.15f)
                        )
                    )
                }
            }
        }
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
        ) {
            items[selectedIndex].content(viewModel)
        }
    }
}
