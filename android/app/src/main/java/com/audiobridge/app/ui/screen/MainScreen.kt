package com.audiobridge.app.ui.screen

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.audiobridge.app.service.ConnectionState
import com.audiobridge.app.settings.RecentConnection
import com.audiobridge.app.ui.theme.AccentPrimary
import com.audiobridge.app.ui.theme.AccentSecondary
import com.audiobridge.app.ui.theme.AccentWarning
import com.audiobridge.app.ui.theme.SurfaceDark
import com.audiobridge.app.ui.theme.TextPrimary
import com.audiobridge.app.ui.theme.TextSecondary
import com.audiobridge.app.viewmodel.MainViewModel
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable
fun MainScreen(viewModel: MainViewModel) {
    val connectionState by viewModel.connectionState.collectAsState()
    val clientName by viewModel.clientName.collectAsState()
    val connectionStatusText by viewModel.connectionStatusText.collectAsState()
    val audioLevel by viewModel.audioLevel.collectAsState()
    val recentConnections by viewModel.recentConnections.collectAsState()
    val pendingFavourite by viewModel.pendingFavourite.collectAsState()
    val isScanning by viewModel.isScanning.collectAsState()
    val snackbarMessage by viewModel.snackbarMessage.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    var manualIp by remember { mutableStateOf("") }

    LaunchedEffect(snackbarMessage) {
        snackbarMessage?.let {
            snackbarHostState.showSnackbar(it)
            viewModel.clearSnackbar()
        }
    }

    Box(modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Spacer(modifier = Modifier.height(48.dp))

            Column(
                modifier = Modifier.fillMaxWidth(),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                ConnectionPulseIndicator(state = connectionState)

                Spacer(modifier = Modifier.height(24.dp))

                Text(
                    text = when (connectionState) {
                        ConnectionState.DISCONNECTED -> "Disconnesso"
                        ConnectionState.CONNECTING -> "Connessione in corso…"
                        ConnectionState.CONNECTED -> "Connesso"
                        ConnectionState.RECONNECTING -> "Riconnessione…"
                    },
                    style = MaterialTheme.typography.headlineSmall,
                    color = when (connectionState) {
                        ConnectionState.DISCONNECTED -> Color.Gray
                        ConnectionState.CONNECTING -> AccentWarning
                        ConnectionState.CONNECTED -> Color(0xFF4CAF50)
                        ConnectionState.RECONNECTING -> AccentWarning
                    },
                    fontWeight = FontWeight.Bold
                )

                if (clientName.isNotEmpty()) {
                    Text(
                        text = clientName,
                        style = MaterialTheme.typography.bodySmall,
                        color = TextSecondary
                    )
                }

                Spacer(modifier = Modifier.height(8.dp))

                if (connectionState == ConnectionState.CONNECTED) {
                    Text(
                        text = connectionStatusText,
                        style = MaterialTheme.typography.bodyMedium,
                        color = TextSecondary,
                        textAlign = TextAlign.Center
                    )
                }

                Spacer(modifier = Modifier.height(16.dp))

                AudioLevelBar(level = audioLevel)

                Spacer(modifier = Modifier.height(24.dp))

                Button(
                    onClick = {
                        when (connectionState) {
                            ConnectionState.CONNECTED -> viewModel.stopStream()
                            ConnectionState.DISCONNECTED -> {
                                if (manualIp.isNotBlank()) {
                                    viewModel.connectToIp(manualIp)
                                    manualIp = ""
                                } else if (viewModel.targetHost.isNotBlank()) {
                                    viewModel.startStream()
                                } else {
                                    viewModel.startDiscovery()
                                }
                            }
                            else -> {}
                        }
                    },
                    enabled = connectionState == ConnectionState.CONNECTED ||
                            (connectionState == ConnectionState.DISCONNECTED &&
                             (manualIp.isNotBlank() || viewModel.targetHost.isNotBlank())),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = if (connectionState == ConnectionState.CONNECTED)
                            Color(0xFFE53935) else AccentPrimary
                    ),
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(56.dp),
                    shape = RoundedCornerShape(16.dp)
                ) {
                    Text(
                        text = if (connectionState == ConnectionState.CONNECTED) "Disconnetti"
                        else "Connetti",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                }

                if (connectionState == ConnectionState.DISCONNECTED) {
                    Spacer(modifier = Modifier.height(16.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        OutlinedTextField(
                            value = manualIp,
                            onValueChange = { manualIp = it },
                            label = { Text("IP manuale") },
                            singleLine = true,
                            modifier = Modifier.weight(1f),
                            shape = RoundedCornerShape(14.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        IconButton(onClick = { viewModel.startDiscovery() }) {
                            Icon(
                                imageVector = if (isScanning) Icons.Default.Refresh
                                    else Icons.Default.Search,
                                contentDescription = "Cerca dispositivi",
                                tint = AccentPrimary
                            )
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(32.dp))

            RecentConnectionsSection(
                connections = recentConnections,
                onTap = { conn -> viewModel.connectToRecent(conn) },
                onRemove = { host -> viewModel.removeRecent(host) }
            )

            Spacer(modifier = Modifier.height(16.dp))
        }

        if (pendingFavourite != null) {
            val (favName, favHost) = pendingFavourite!!
            AlertDialog(
                onDismissRequest = { viewModel.dismissPendingFavourite() },
                title = {
                    Text("Salva nei preferiti?", fontWeight = FontWeight.Bold)
                },
                text = {
                    Text("Vuoi salvare $favName ($favHost) nei preferiti?")
                },
                confirmButton = {
                    TextButton(onClick = { viewModel.savePendingFavourite() }) {
                        Text("Salva", fontWeight = FontWeight.Bold)
                    }
                },
                dismissButton = {
                    TextButton(onClick = { viewModel.dismissPendingFavourite() }) {
                        Text("Ignora")
                    }
                }
            )
        }

        SnackbarHost(
            hostState = snackbarHostState,
            modifier = Modifier.align(Alignment.BottomCenter).padding(16.dp)
        )
    }
}

@Composable
private fun RecentConnectionsSection(
    connections: List<RecentConnection>,
    onTap: (RecentConnection) -> Unit,
    onRemove: (String) -> Unit
) {
    Text(
        text = "Connessioni recenti",
        style = MaterialTheme.typography.titleMedium,
        color = TextPrimary,
        fontWeight = FontWeight.Bold,
        modifier = Modifier.fillMaxWidth()
    )

    Spacer(modifier = Modifier.height(12.dp))

    if (connections.isEmpty()) {
        Text(
            text = "Nessun dispositivo recente. Inserisci un IP manualmente per la prima connessione.",
            style = MaterialTheme.typography.bodySmall,
            color = TextSecondary,
            modifier = Modifier.fillMaxWidth()
        )
    } else {
        connections.forEach { conn ->
            RecentConnectionCard(
                connection = conn,
                onTap = { onTap(conn) },
                onRemove = { onRemove(conn.host) }
            )
            Spacer(modifier = Modifier.height(8.dp))
        }
    }
}

@Composable
private fun RecentConnectionCard(
    connection: RecentConnection,
    onTap: () -> Unit,
    onRemove: () -> Unit
) {
    val dateFormat = SimpleDateFormat("dd/MM/yyyy HH:mm", Locale.getDefault())
    val formattedDate = dateFormat.format(Date(connection.timestamp))

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onTap),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(
            containerColor = SurfaceDark
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(start = 16.dp, end = 4.dp, top = 12.dp, bottom = 12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = connection.name,
                    style = MaterialTheme.typography.titleSmall,
                    color = TextPrimary,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = connection.host,
                    style = MaterialTheme.typography.bodySmall,
                    color = TextSecondary
                )
                Text(
                    text = "Ultima connessione: $formattedDate",
                    style = MaterialTheme.typography.labelSmall,
                    color = AccentSecondary
                )
            }
            IconButton(onClick = onRemove) {
                Icon(
                    imageVector = Icons.Default.Close,
                    contentDescription = "Rimuovi",
                    tint = TextSecondary
                )
            }
        }
    }
}

@Composable
fun ConnectionPulseIndicator(state: ConnectionState) {
    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val pulseAlpha by infiniteTransition.animateFloat(
        initialValue = 0.3f,
        targetValue = 1.0f,
        animationSpec = infiniteRepeatable(
            animation = tween(1000),
            repeatMode = RepeatMode.Reverse
        ),
        label = "pulseAlpha"
    )

    val targetColor = when (state) {
        ConnectionState.DISCONNECTED -> Color.Gray
        ConnectionState.CONNECTING -> AccentPrimary
        ConnectionState.CONNECTED -> Color(0xFF4CAF50)
        ConnectionState.RECONNECTING -> AccentWarning
    }

    val color by animateColorAsState(
        targetValue = targetColor,
        label = "pulseColor"
    )

    val alpha = when (state) {
        ConnectionState.CONNECTING, ConnectionState.RECONNECTING -> pulseAlpha
        else -> 1.0f
    }

    Card(
        shape = RoundedCornerShape(90.dp),
        colors = CardDefaults.cardColors(
            containerColor = SurfaceDark
        ),
        modifier = Modifier.size(180.dp)
    ) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Canvas(modifier = Modifier.size(100.dp)) {
                drawCircle(
                    color = color.copy(alpha = alpha * 0.2f),
                    radius = size.minDimension / 2
                )
                drawCircle(
                    color = color.copy(alpha = alpha),
                    radius = size.minDimension / 3,
                    center = center
                )
                drawCircle(
                    color = color.copy(alpha = alpha * 0.8f),
                    radius = size.minDimension / 6,
                    center = center
                )
            }
        }
    }
}

@Composable
fun AudioLevelBar(level: Float) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .height(48.dp),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(
            containerColor = SurfaceDark
        )
    ) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(4.dp)
        ) {
            val barColor = when {
                level < 0.5f -> Color(0xFF4CAF50)
                level < 0.8f -> AccentWarning
                else -> Color(0xFFE53935)
            }
            Box(
                modifier = Modifier
                    .fillMaxWidth(level)
                    .fillMaxHeight()
                    .background(
                        color = barColor,
                        shape = RoundedCornerShape(8.dp)
                    )
            )
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 12.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = "Livello Audio",
                    style = MaterialTheme.typography.labelSmall,
                    color = TextSecondary
                )
                Text(
                    text = "${(level * 100).toInt()}%",
                    style = MaterialTheme.typography.labelSmall,
                    color = TextPrimary
                )
            }
        }
    }
}
