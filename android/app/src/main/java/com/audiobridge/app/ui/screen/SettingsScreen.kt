package com.audiobridge.app.ui.screen

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.audiobridge.app.ui.theme.AccentPrimary
import com.audiobridge.app.ui.theme.SurfaceDark
import com.audiobridge.app.ui.theme.TextPrimary
import com.audiobridge.app.ui.theme.TextSecondary
import com.audiobridge.app.viewmodel.MainViewModel
import kotlinx.coroutines.launch

@Composable
fun SettingsScreen(viewModel: MainViewModel) {
    val coroutineScope = rememberCoroutineScope()
    val jitterBufferSize by viewModel.jitterBufferSize.collectAsState()
    val keepBackground by viewModel.keepBackground.collectAsState()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .padding(24.dp)
    ) {
        Text(
            text = "Impostazioni",
            style = MaterialTheme.typography.titleLarge,
            color = TextPrimary,
            fontWeight = FontWeight.Bold
        )

        Spacer(modifier = Modifier.height(24.dp))

        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(14.dp),
            colors = CardDefaults.cardColors(
                containerColor = SurfaceDark
            )
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(
                    text = "Buffer Jitter",
                    style = MaterialTheme.typography.titleMedium,
                    color = TextPrimary,
                    fontWeight = FontWeight.SemiBold
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "Frame di buffer: $jitterBufferSize (${jitterBufferSize * 10}ms)",
                    style = MaterialTheme.typography.bodySmall,
                    color = TextSecondary
                )
                Spacer(modifier = Modifier.height(8.dp))
                Slider(
                    value = jitterBufferSize.toFloat(),
                    onValueChange = { value ->
                        viewModel.setJitterBufferSize(value.toInt())
                    },
                    valueRange = 1f..20f,
                    steps = 19,
                    colors = SliderDefaults.colors(
                        thumbColor = AccentPrimary,
                        activeTrackColor = AccentPrimary
                    )
                )
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text("1", style = MaterialTheme.typography.labelSmall, color = TextSecondary)
                    Text("20", style = MaterialTheme.typography.labelSmall, color = TextSecondary)
                }
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(14.dp),
            colors = CardDefaults.cardColors(
                containerColor = SurfaceDark
            )
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "Riproduzione in background",
                        style = MaterialTheme.typography.titleMedium,
                        color = TextPrimary,
                        fontWeight = FontWeight.SemiBold
                    )
                    Text(
                        text = "Mantieni l\'audio attivo anche con l\'app in secondo piano",
                        style = MaterialTheme.typography.bodySmall,
                        color = TextSecondary
                    )
                }
                Switch(
                    checked = keepBackground,
                    onCheckedChange = { enabled ->
                        coroutineScope.launch {
                            viewModel.setKeepBackground(enabled)
                        }
                    },
                    colors = SwitchDefaults.colors(
                        checkedThumbColor = AccentPrimary,
                        checkedTrackColor = AccentPrimary.copy(alpha = 0.3f)
                    )
                )
            }
        }
    }
}
